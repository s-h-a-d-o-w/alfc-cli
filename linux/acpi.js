const { execSync } = require('child_process');

// Read calls also always expect 3 arguments.
// IF something really needs to be specified, it's packed into the 3rd argument, like with write.
// Otherwise, it's simply not used.
function readCall(commandId) {
  execSync(`echo '\\_SB_.PCI0.AMW0.WMBC 0 ${commandId} 0' | sudo tee /proc/acpi/call`);
  return execSync("sudo cat /proc/acpi/call", { encoding: 'utf8' });
}

function readCallBoolean(commandId) {
  return readCall(commandId).startsWith("0x1");
}

function readCallInt(commandId) {
  return parseInt(readCall(commandId), 16);
}

function readCallLittleEndianWord(commandId) {
  const result = readCall(commandId).split('');
  const swappedBytes = '0x' + result[4] + result[5] + result[2] + result[3];
  return parseInt(swappedBytes, 16);
}

function setCall(commandId, hexString) {
  execSync(`echo '\\_SB_.PCI0.AMW0.WMBD 0 ${commandId} ${hexString}' | sudo tee /proc/acpi/call`);
}

module.exports = {
  readCall,
  readCallBoolean,
  readCallInt,
  readCallLittleEndianWord,
  setCall,
}