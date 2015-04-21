""" This module defines the paths that Whoop will use
    to run the various tools that it depends on.

    These paths must be absolute paths.
"""
import sys

rootDir = "/Users/pantazis/workspace/whoop"

# path to the directory containing the Whoop project
whoopDir = rootDir + "/whoop"

# path to the llvm source directory
llvmSrcDir = rootDir + "/llvm_and_clang/src"

# path to the llvm binary directory
llvmBinDir = rootDir + "/llvm_and_clang/build/bin"

# path containing the llvm libraries
llvmLibDir = rootDir + "/llvm_and_clang/build/lib"

# path to the directory containing the chauffeur clang tool
chauffeurDir = rootDir + "/chauffeur/build"

# path to the SMACK libraries
smackSrcDir = rootDir + "/smack/install/include"

# path to the directory where the SMACK executable can be found
smackBinDir = rootDir + "/smack/install/bin"

# path to the directory where the Corral executable can be found
corralBinDir = rootDir + "/corral/bin/debug"

# path to the directory containing the Whoop binaries
whoopBinDir = rootDir + "/whoop/Binaries"

# path to the directory containing the z3 executable
z3BinDir = rootDir + "/z3/build"

# path to the directory containing the cvc4 executable
cvc4BinDir = rootDir + "/cvc4/install/bin"

def init(prefixPath):
  """This method does nothing"""
  pass
