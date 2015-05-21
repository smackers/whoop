Whoop
====================
Whoop is a symbolic data race analyzer for Linux device drivers.

## Prerequisites
1. [LLVM](http://llvm.org) 3.5
2. [SMACK](https://github.com/smackers/smack) 1.5
3. [Z3](https://github.com/Z3Prover/z3) 4.3.2
4. [Corral](https://corral.codeplex.com)
5. [Chauffeur](https://github.com/mc-imperial/chauffeur)

## Build instructions
1. Clone this project.
2. Compile using Visual Studio or Mono.

We also have [vagrant](https://www.vagrantup.com) support for building a virtual machine with the toolchain installed. To do this use the following from the project's root directory:

```
vagrant up
```

## How to use

The input to Whoop is a Linux driver. To use Whoop do the following:

```
.\whoop.py ${DRIVER}.c
```

The use Corral for finding precise bugs do the following:

```
.\whoop.py ${DRIVER}.c --find-bugs --yield-race-check
```

## Tool options

Use --verbose for verbose mode. Use --time for timing information.
