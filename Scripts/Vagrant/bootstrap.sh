#!/usr/bin/env bash

echo Getting updates ...

sudo apt-get -y update
sudo apt-get install -y g++
sudo apt-get install -y make
sudo apt-get install -y python-software-properties python
sudo apt-get install -y automake autoconf
sudo apt-get install -y wget git subversion mercurial

export PROJECT_ROOT=/vagrant
export BUILD_ROOT=/home/vagrant/whoop
export CMAKE_VERSION=2.8.8
export MONO_VERSION=3.0.7
export LLVM_RELEASE=34

mkdir -p ${BUILD_ROOT}
cd ${BUILD_ROOT}

echo Getting CMAKE ${CMAKE_VERSION} ...

wget http://www.cmake.org/files/v2.8/cmake-${CMAKE_VERSION}-Linux-i386.tar.gz

echo Unpacking CMAKE ${CMAKE_VERSION} ...

tar zxvf cmake-${CMAKE_VERSION}-Linux-i386.tar.gz
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
make -j4
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
make -j4

echo Getting SMACK ...

cd ${BUILD_ROOT}
git clone https://github.com/smackers/smack.git ${BUILD_ROOT}/smack/src

echo Building SMACK ...

mkdir -p ${BUILD_ROOT}/smack/build
cd ${BUILD_ROOT}/smack/build
cmake -D LLVM_CONFIG=${BUILD_ROOT}/llvm_and_clang/build/bin -D CMAKE_BUILD_TYPE=Release -D CMAKE_INSTALL_PREFIX=${BUILD_ROOT}/smack/install ../src
make -j4
make install

echo Getting Z3 ...

cd ${BUILD_ROOT}
git clone https://git01.codeplex.com/z3

echo Building Z3 ...

cd ${BUILD_ROOT}/z3
autoconf
./configure
python scripts/mk_make.py
cd build
make -j4
ln -s z3 z3.exe

echo Getting CVC4 ...

cd ${BUILD_ROOT}
git clone https://github.com/CVC4/CVC4.git ${BUILD_ROOT}/CVC4/src

echo Building CVC4 ...

cd ${BUILD_ROOT}/CVC4/src
MACHINE_TYPE="x86_64" contrib/get-antlr-3.4
./autogen.sh
export ANTLR=${BUILD_ROOT}/CVC4/src/antlr-3.4/bin/antlr3
./configure --with-antlr-dir=${BUILD_ROOT}/CVC4/src/antlr-3.4 \
	--prefix=${BUILD_ROOT}/CVC4/install \
	--best --enable-gpl \
	--disable-shared --enable-static
make -j4
make install
cd ${BUILD_ROOT}/CVC4/install/bin
ln -s cvc4 cvc4.exe

echo Building CHAUFFEUR ...

cp -a ${PROJECT_ROOT}/. ${BUILD_ROOT}/whoop/
cd ${BUILD_ROOT}/whoop
mkdir -p ${BUILD_ROOT}/whoop/FrontEndPlugin/build
cd ${BUILD_ROOT}/whoop/FrontEndPlugin/build
cmake -D LLVM_CONFIG=${BUILD_ROOT}/llvm_and_clang/build/bin -D CMAKE_BUILD_TYPE=Release ../src
make -j4

echo Building WHOOP ...

cd ${BUILD_ROOT}/whoop
xbuild /p:TargetFrameworkProfile="" /p:Configuration=Debug whoop.sln

echo Configuring WHOOP ...

mv findtools.py findtools-backup.py
cp /Scripts/Vagrant/findtools.vagrant.py findtools.py

echo Done ...
