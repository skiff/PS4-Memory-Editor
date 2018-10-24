# PS4-Memory-Editor
ELF Loader and Memory editor for PS4s on 4.55 and 5.05 using jkpatch

# Features
Realtime Memory Editing
Memory Dumping to bin
PS4 ELF Injector
PS4 Process Viewer
Socket Listener for Output (Not Working)

# Usage
1) Enter PS4 IP and select version
2) Send Payload to PS4 webkit
3) Load game
4) Update and select game process

# Extra Information
- with ASLR disabled game processes start at 0x400000
- ELFs cannot be unloaded

# Credits
- golden for jkpatch
- jariq for HexBox winform
- ImJtagModz for socket help
