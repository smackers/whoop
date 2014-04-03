#!/usr/bin/env bash

sudo apt-get update
# sudo apt-get install -y python-software-properties
# sudo apt-get install -y build-essential
sudo apt-get install -y automake git subversion
# sudo apt-get install -y mono-complete mono-xbuild

# sudo add-apt-repository ppa:keks9n/monodevelop-latest
# sudo apt-get update
# sudo apt-get install -y monodevelop-latest
# apt-get install -y g++

export BUILD_ROOT=/vagrant/build

# cd ${BUILD_ROOT}
# export MONO_VERSION=3.0.7
# wget http://download.mono-project.com/sources/mono/mono-${MONO_VERSION}.tar.bz2
# tar jxf mono-${MONO_VERSION}.tar.bz2
# rm mono-${MONO_VERSION}.tar.bz2
# cd ${BUILD_ROOT}/mono-${MONO_VERSION}
# ./configure --prefix=${BUILD_ROOT}/local --with-large-heap=yes --enable-nls=no
# make
# make install
# 
# export PATH=${BUILD_ROOT}/local/bin:$PATH

export LLVM_RELEASE=34
mkdir -p ${BUILD_ROOT}/llvm_and_clang
cd ${BUILD_ROOT}/llvm_and_clang
svn co -q http://llvm.org/svn/llvm-project/llvm/branches/release_${LLVM_RELEASE} src
cd ${BUILD_ROOT}/llvm_and_clang/src/tools
svn co -q http://llvm.org/svn/llvm-project/cfe/branches/release_${LLVM_RELEASE} clang
cd ${BUILD_ROOT}/llvm_and_clang/src/projects
svn co -q http://llvm.org/svn/llvm-project/compiler-rt/branches/release_${LLVM_RELEASE} compiler-rt
svn cleanup

mkdir -p ${BUILD_ROOT}/llvm_and_clang/build
cd ${BUILD_ROOT}/llvm_and_clang/build
cmake -D CMAKE_BUILD_TYPE=Release ../src
make

cd ${BUILD_ROOT}
git clone https://github.com/smackers/smack.git ${BUILD_ROOT}/smack/src
mkdir ${BUILD_ROOT}/smack/build
cd ${BUILD_ROOT}/smack/build
cmake -D LLVM_CONFIG=${BUILD_ROOT}/llvm_and_clang/build/bin -D CMAKE_BUILD_TYPE=Release -D CMAKE_INSTALL_PREFIX=${BUILD_ROOT}/smack/install ../src
make
make install

cd ${BUILD_ROOT}
git clone ssh://pd1113@svnuser.doc.ic.ac.uk/vol/multicore/git/whoop.git ${BUILD_ROOT}/whoop
mkdir ${BUILD_ROOT}/whoop/FrontEndPlugin/build
cd ${BUILD_ROOT}/whoop/FrontEndPlugin/build
cmake -D LLVM_CONFIG=${BUILD_ROOT}/llvm_and_clang/build/bin -D CMAKE_BUILD_TYPE=Release ../src
make

cd ${BUILD_ROOT}/whoop
xbuild /p:TargetFrameworkProfile="" /p:Configuration=Debug whoop.sln