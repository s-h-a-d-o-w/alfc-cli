const fs = require('fs');
const path = require('path');

const {
  readCall,
  readCallBoolean,
  readCallInt,
  readCallLittleEndianWord,
  setCall,
} = require('./acpi');

const WAIT_RAMP_DOWN_CYCLES = 10;
const WAIT_RAMP_UP_CYCLES = 1;
const CYCLE_DURATION = 1000;
const TEMP_POLL_INTERVAL = 200;

function fanSpeedToPercent(speed) {
  return (speed / 229.0) * 100;
}

function fanPercentToSpeed(percent) {
  return (percent / 100.0) * 229;
}

// Each number in the provided array is assumed to be a byte.
function numbersToHexString(numbers) {
  return '0x' + Buffer.from(numbers).toString('hex');
}

function printStatus() {
  // GetFanIndexValue
  // const index = 3;
  // console.log(`Fan Index ${index} Value: ${readCall("0x68", numbersToHexString([index]))}`);

  // GetAutoFanStatus
  console.log('Auto Fan: ' + readCallBoolean("0x71"));
  // // GetStepFanStatus
  console.log('Step Fan: ' + readCallBoolean("0x67"));
  // // GetFixedFanStatus
  console.log('Fixed Fan: ' + readCallBoolean("0x6a"));
  // // GetFixedFanSpeed
  console.log('Fixed Fan Speed: ' + Math.round(fanSpeedToPercent(parseInt(readCall("0x6b")))) + '%');

  // // GetFixedFanStatus
  console.log('RPM1: ' + readCallLittleEndianWord("0xe4"));
  // // GetFixedFanStatus
  console.log('RPM2: ' + readCallLittleEndianWord("0xe5"));

  // GetAIBoostStatus
  console.log('GPU Boost Status: ' + readCallBoolean("0x81"));
}

// Both CPU and GPU fan are set to the same speed 
// due to the shared heat pipes.
function setFixedFan(percent) {
  const speed = fanPercentToSpeed(percent);

  // SetFixedFanSpeed
  setCall('0x6b', numbersToHexString([speed]));
  // SetGPUFanDuty
  setCall('0x47', numbersToHexString([speed]));
}

function readProfile(profilePath) {
  const file = fs.readFileSync(profilePath, { encoding: 'utf8' });

  const fanTable = [];
  for (const line of file.split('\n')) {
    const tuple = line.split(':');
    if (tuple.length === 2) {
      fanTable.push(tuple);
    }
  }

  return fanTable;
}

function fanControl(profileCPU, profileGPU, isDebug) {
  const cpuTable = readProfile(profileCPU);
  const gpuTable = readProfile(profileGPU);

  // SetQuietMode(0);
  setCall('0x58', '0');
  // SetAutoFanStatus(false);
  setCall('0x71', '0');
  // SetStepFanStatus(false);
  setCall('0x67', '0');
  // SetFixedFanStatus(true)
  setCall('0x6a', '0x1');

  // Find highest entry that isn't larger than provided temp,
  // assuming that fan table entries in profiles are ascending.
  function findHighestMatch(temperature, table) {
    let highestMatch = table[0];

    for (const entry of table) {
      if (entry[0] <= temperature) {
        highestMatch = entry;
      } else {
        break;
      }
    }

    return highestMatch;
  }

  let appliedSpeed = 0;
  let currRampDownCycle = 0;
  let currRampUpCycle = 0;
  setInterval(async () => {
    const {avgCPUTemp, avgGPUTemp} = await new Promise((resolve) => {
      const CPUTemps = [];
      const GPUTemps = [];
      const pushTemps = () => {
        // getCpuTemperature
        const currCPUTemp = readCallInt("0xe1");
        // getGpuTemperature1
        const currGPUTemp1 = readCallInt("0xe2");
        // getGpuTemperature2
        const currGPUTemp2 = readCallInt("0xe3");
        const currGPUTemp = Math.max(currGPUTemp1, currGPUTemp2);
        // isDebug && console.log(`CPU and GPU1/GPU2 temperatures: ${currCPUTemp} ${currGPUTemp1}/${currGPUTemp2}`);

        CPUTemps.push(currCPUTemp);
        GPUTemps.push(currGPUTemp);

        if(CPUTemps.length === Math.round((CYCLE_DURATION - TEMP_POLL_INTERVAL) / TEMP_POLL_INTERVAL)) {
          resolve({
            avgCPUTemp: CPUTemps.reduce((sum, temp) => sum + temp) / CPUTemps.length,
            avgGPUTemp: GPUTemps.reduce((sum, temp) => sum + temp) / GPUTemps.length
          });
        } else {
          setTimeout(pushTemps, TEMP_POLL_INTERVAL);
        }
      };
      pushTemps();
    });
    
    const highestMatchCPU = findHighestMatch(avgCPUTemp, cpuTable);
    const highestMatchGPU = findHighestMatch(avgGPUTemp, gpuTable);

    // Target speed is whichever one of the two is higher because 
    // of the mostly shared heat pipes.
    const target = Math.max(highestMatchCPU[1], highestMatchGPU[1]);
    // isDebug && console.log('Target %: ' + target);

    if (appliedSpeed < target) {
      if(currRampUpCycle === WAIT_RAMP_UP_CYCLES) {
        isDebug && console.log(`Average CPU and GPU temperatures: ${avgCPUTemp}/${avgGPUTemp}`);
        isDebug && console.log('Applying %: ' + target);
        setFixedFan(target);

        currRampDownCycle = 0;
        currRampUpCycle = 0;
        appliedSpeed = target;
      } else {
        currRampUpCycle++;
      }
    } else if(target < appliedSpeed) {
      // Make fan behavior less erratic by waiting a few cycles until we 
      // ramp down.
      if(currRampDownCycle === WAIT_RAMP_DOWN_CYCLES) {
        isDebug && console.log(`Average CPU and GPU temperatures: ${avgCPUTemp}/${avgGPUTemp}`);
        isDebug && console.log('Applying %: ' + target);
        setFixedFan(target);

        currRampDownCycle = 0;
        currRampUpCycle = 0;
        appliedSpeed = target;
      } else {
        currRampDownCycle++;
      }
    } else {
      currRampDownCycle = 0;
      currRampUpCycle = 0;
    }
  }, CYCLE_DURATION);
}

// ====================================================
// This is roughly how setting the fan curve just like 
// in Windows should theoretically work.
// ====================================================
function setFanCurve() {
  // SetCurrentFanStep(0);
  setCall('0x66', '0');
  // SetQuietMode(0);
  setCall('0x58', '0');
  // SetAutoFanStatus(false);
  setCall('0x71', '0');
  // SetStepFanStatus(true);
  setCall('0x67', '1');
  // SetFixedFanStatus(false)
  setCall('0x6a', '0');

  const fanIndex = 4;
  const temperature = 85;
  const speed = Math.ceil(fanPercentToSpeed(75));
  const hexString = numbersToHexString([i, temperature, speed]);
  execSync(`echo '\\_SB_.PCI0.AMW0.WMBD 0 0x68 ${hexString}' | sudo tee /proc/acpi/call`);

  // // Doesn't matter what is supplied in the first byte, nothing works...
  // for(let i = 0; i < 255; i++) {
  //     const hexString = numbersToHexString([i, 41, 42]);
  //     execSync(`echo '\\_SB_.PCI0.AMW0.WMBD 0 0x68 ${hexString}' | sudo tee /proc/acpi/call`);
  // }
}
// ====================================================

if (process.argv.length < 3) {
  console.log('You need to specify either a profile file name or "--status"');
} else {
  if (process.argv[2] === "--status") {
    printStatus();
  } else {
    const isDebug = process.argv[2] === "--debug";
    fanControl(
      path.join(__dirname, process.argv[isDebug ? 3 : 2]),
      path.join(__dirname, process.argv[isDebug ? 4 : 3]),
      isDebug
    );
    console.log('Aorus Fan Control is running');
  }
}
