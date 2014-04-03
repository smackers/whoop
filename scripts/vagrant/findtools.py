""" This module defines the paths that Whoop will use
    to run the various tools that it depends on.

    These paths must be absolute paths.
"""
import sys

rootDir = "/vagrant"
buildDir = "/vagrant/build"

# path to the directory containing the Whoop project
whoopDir = rootDir

# path to the llvm source directory
llvmSrcDir = buildDir + "/llvm_and_clang/src"

# path to the llvm binary directory
llvmBinDir = buildDir + "/llvm_and_clang/build/bin"

# path containing the llvm libraries
llvmLibDir = buildDir + "/llvm_and_clang/build/lib"

# path to the directory containing the chauffeur clang tool
chauffeurDir = rootDir + "/FrontEndPlugin/build"

# path to the SMACK source directory
smackSrcDir = buildDir + "/smack/src"

# path to the directory where the SMACK executable can be found
smackBinDir = buildDir + "/smack/install/bin"

#The path to the directory containing the Whoop binaries
whoopBinDir = buildDir + "/whoop/Binaries"

# path to the directory containing the z3 executable
z3BinDir = buildDir + "/z3/build"

# path to the directory containing the cvc4 executable
cvc4BinDir = buildDir + "/cvc4/install/bin"

def init(prefixPath):
  """This method does nothing"""
  pass
