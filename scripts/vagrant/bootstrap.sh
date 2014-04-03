#!/usr/bin/env bash

echo Getting updates ...

sudo apt-get -y update
sudo apt-get install -y python-software-properties python g++
sudo apt-get install -y automake make
sudo apt-get install -y wget git subversion

export PROJECT_ROOT=/vagrant
export BUILD_ROOT=/vagrant/build
export CMAKE_VERSION=2.8.8
export MONO_VERSION=3.0.7
export LLVM_RELEASE=34

mkdir -p ${BUILD_ROOT}
cd ${BUILD_ROOT}

echo Getting CMAKE ${CMAKE_VERSION} ...

http://www.cmake.org/files/v2.8/cmake-${CMAKE_VERSION}-Linux-i386.tar.gz

echo Unpacking CMAKE ${CMAKE_VERSION} ...

tar jxf cmake-${CMAKE_VERSION}-Linux-i386.tar.gz
rm cmake-${CMAKE_VERSION}-Linux-i386.tar.gz

export PATH=${BUILD_ROOT}/cmake-${CMAKE_VERSION}-Linux-i386/bin:$PATH

echo Getting MONO ${MONO_VERSION} ...

wget http://download.mono-project.com/sources/mono/mono-${MONO_VERSION}.tar.bz2

echo Unpacking MONO ${MONO_VERSION} ...

tar jxf mono-${MONO_VERSION}.tar.bz2
rm mono-${MONO_VERSION}.tar.bz2

echo Building MONO ${MONO_VERSION} ...

cd ${BUILD_ROOT}/mono-${MONO_VERSION}
./configure --prefix=${BUILD_ROOT}/local --with-large-heap=yes --enable-nls=no
make
make install

export PATH=${BUILD_ROOT}/local/bin:$PATH

echo Getting LLVM ${LLVM_RELEASE} ...

mkdir -p ${BUILD_ROOT}/llvm_and_clang
cd ${BUILD_ROOT}/llvm_and_clang
svn co http://llvm.org/svn/llvm-project/llvm/branches/release_${LLVM_RELEASE} src
cd ${BUILD_ROOT}/llvm_and_clang/src/tools
svn co http://llvm.org/svn/llvm-project/cfe/branches/release_${LLVM_RELEASE} clang
cd ${BUILD_ROOT}/llvm_and_clang/src/projects
svn co http://llvm.org/svn/llvm-project/compiler-rt/branches/release_${LLVM_RELEASE} compiler-rt

echo Building LLVM ${LLVM_RELEASE} ...

mkdir -p ${BUILD_ROOT}/llvm_and_clang/build
cd ${BUILD_ROOT}/llvm_and_clang/build
cmake -D CMAKE_BUILD_TYPE=Release ../src
make

echo Getting SMACK ...

cd ${BUILD_ROOT}
git clone https://github.com/smackers/smack.git ${BUILD_ROOT}/smack/src

echo Building SMACK ...

mkdir ${BUILD_ROOT}/smack/build
cd ${BUILD_ROOT}/smack/build
cmake -D LLVM_CONFIG=${BUILD_ROOT}/llvm_and_clang/build/bin -D CMAKE_BUILD_TYPE=Release -D CMAKE_INSTALL_PREFIX=${BUILD_ROOT}/smack/install ../src
make
make install

echo Building CHAUFFEUR ...

mkdir ${PROJECT_ROOT}/FrontEndPlugin/build
cd ${PROJECT_ROOT}/FrontEndPlugin/build
cmake -D LLVM_CONFIG=${BUILD_ROOT}/llvm_and_clang/build/bin -D CMAKE_BUILD_TYPE=Release ../src
make

echo Building WHOOP ...

cd ${PROJECT_ROOT}
xbuild /p:TargetFrameworkProfile="" /p:Configuration=Debug whoop.sln

echo Finalising build ...

mv findtools.py findtools-backup.py
cp ${PROJECT_ROOT}/scripts/vagrant/findtools.py findtools.py

echo Done ...
