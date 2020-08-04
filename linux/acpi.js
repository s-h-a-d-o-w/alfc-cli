const { execSync } = require('child_process');

// Read calls also always expect 3 arguments.
// IF something really needs to be specified, it's packed into the 3rd argument, like with write.
// Otherwise, it's simply not used.
// @return Multiple values are returned in a single number, little endian!
function readCall(commandId, hexString = 0) {
  execSync(`echo '\\_SB_.PCI0.AMW0.WMBC 0 ${commandId} ${hexString}' | tee /proc/acpi/call`);
  return execSync("cat /proc/acpi/call", { encoding: 'utf8' });
}

function readCallBoolean(commandId) {
  return readCall(commandId).startsWith("0x1");
}

function readCallInt(commandId) {
  return parseInt(readCall(commandId), 16);
}

function readCallLittleEndianWord(commandId) {
  const result = readCall(commandId);
  
  const resultWithoutTerminator = result.substr(0, result.length - 1);
  if(resultWithoutTerminator === '0x0') {
    return 0;
  }

  const splitResult = result.split('');
  const swappedBytes = '0x' + splitResult[4] + splitResult[5] + splitResult[2] + splitResult[3];
  return parseInt(swappedBytes, 16);
}

function setCall(commandId, hexString) {
  execSync(`echo '\\_SB_.PCI0.AMW0.WMBD 0 ${commandId} ${hexString}' | tee /proc/acpi/call`);
}

module.exports = {
  readCall,
  readCallBoolean,
  readCallInt,
  readCallLittleEndianWord,
  setCall,
}