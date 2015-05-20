#!/usr/bin/env bash

echo $'\n==================='
echo $'Getting updates ...'
echo $'===================\n'

sudo apt-get -y update
sudo apt-get install -y g++
sudo apt-get install -y make
sudo apt-get install -y python-software-properties python
sudo apt-get install -y automake autoconf
sudo apt-get install -y libtool libgmp-dev libcln-dev
sudo apt-get install -y wget git subversion mercurial
sudo apt-get install -y gettext zlib1g-dev asciidoc libcurl4-openssl-dev
sudo apt-get install -y git

sudo add-apt-repository -y ppa:ubuntu-toolchain-r/test
sudo apt-get -y update
sudo apt-get install -y g++-4.9

export PROJECT_ROOT=/vagrant
export BUILD_ROOT=/home/vagrant/whoop
export CMAKE_VERSION=2.8.8
export MONO_VERSION=3.12.1
export LLVM_RELEASE=35
export SMACK_RELEASE=v.1.5.0
export Z3_RELEASE=z3-4.1.1
export BOOGIE_RELEASE=7f7e70772d04b1c574609a5504c9160ca01aca67
export CORRAL_RELEASE=3aa62d7425b57295f698c6f47d3ce1910f5f5f8d

mkdir -p ${BUILD_ROOT}

echo $'\n======================'
echo $'Getting latest git ...'
echo $'======================\n'

cd ${BUILD_ROOT}
git clone https://github.com/git/git.git

echo $'\n======================='
echo $'Building latest git ...'
echo $'=======================\n'

cd git
make configure
./configure --prefix=/usr
make all doc
sudo make install install-doc install-html

echo $'\n================='
echo $'Getting CMAKE ...'
echo $'=================\n'

cd ${BUILD_ROOT}
wget http://www.cmake.org/files/v2.8/cmake-${CMAKE_VERSION}-Linux-i386.tar.gz

echo $'\n==================='
echo $'Unpacking CMAKE ...'
echo $'===================\n'

tar zxvf cmake-${CMAKE_VERSION}-Linux-i386.tar.gz
rm cmake-${CMAKE_VERSION}-Linux-i386.tar.gz
export PATH=${BUILD_ROOT}/cmake-${CMAKE_VERSION}-Linux-i386/bin:$PATH

echo $'\n================'
echo $'Getting MONO ...'
echo $'================\n'

cd ${BUILD_ROOT}
wget http://download.mono-project.com/sources/mono/mono-${MONO_VERSION}.tar.bz2

echo $'\n=================='
echo $'Unpacking MONO ...'
echo $'==================\n'

tar jxf mono-${MONO_VERSION}.tar.bz2
rm mono-${MONO_VERSION}.tar.bz2

echo $'\n================='
echo $'Building MONO ...'
echo $'=================\n'

cd ${BUILD_ROOT}/mono-${MONO_VERSION}
./configure --prefix=${BUILD_ROOT}/local --with-large-heap=yes --enable-nls=no
make -j4
make install
export PATH=${BUILD_ROOT}/local/bin:$PATH

echo $'\n================'
echo $'Getting LLVM ...'
echo $'================\n'

mkdir -p ${BUILD_ROOT}/llvm_and_clang
cd ${BUILD_ROOT}/llvm_and_clang
git clone https://github.com/llvm-mirror/llvm.git src
cd ${BUILD_ROOT}/llvm_and_clang/src
git checkout release_${LLVM_RELEASE}
cd ${BUILD_ROOT}/llvm_and_clang/src/tools
git clone https://github.com/llvm-mirror/clang.git clang
cd ${BUILD_ROOT}/llvm_and_clang/src/tools/clang
git checkout release_${LLVM_RELEASE}
cd ${BUILD_ROOT}/llvm_and_clang/src/projects
git clone https://github.com/llvm-mirror/compiler-rt.git compiler-rt
cd ${BUILD_ROOT}/llvm_and_clang/src/projects/compiler-rt
git checkout release_${LLVM_RELEASE}

echo $'\n================='
echo $'Building LLVM ...'
echo $'=================\n'

mkdir -p ${BUILD_ROOT}/llvm_and_clang/build
cd ${BUILD_ROOT}/llvm_and_clang/build
cmake -D CMAKE_BUILD_TYPE=Release -D LLVM_TARGETS_TO_BUILD="X86" ../src
make -j4

echo $'\n================='
echo $'Getting SMACK ...'
echo $'=================\n'

cd ${BUILD_ROOT}
git clone https://github.com/smackers/smack.git ${BUILD_ROOT}/smack/src
cd ${BUILD_ROOT}/smack/src
git checkout tags/${SMACK_RELEASE}

echo $'\n=================='
echo $'Building SMACK ...'
echo $'==================\n'

mkdir -p ${BUILD_ROOT}/smack/build
cd ${BUILD_ROOT}/smack/build
cmake -D LLVM_CONFIG=${BUILD_ROOT}/llvm_and_clang/build/bin -D CMAKE_BUILD_TYPE=Release -D CMAKE_INSTALL_PREFIX=${BUILD_ROOT}/smack/install ../src
make -j4
make install

echo $'\n=============='
echo $'Getting Z3 ...'
echo $'==============\n'

cd ${BUILD_ROOT}
git clone https://github.com/Z3Prover/z3.git
cd ${BUILD_ROOT}/z3
git checkout tags/${Z3_RELEASE}

echo $'\n==============='
echo $'Building Z3 ...'
echo $'===============\n'

cd ${BUILD_ROOT}/z3
python scripts/mk_make.py --prefix=${BUILD_ROOT}/z3/install
cd ${BUILD_ROOT}/z3/build
make -j4
make install
cd ${BUILD_ROOT}/z3/install/bin
ln -s z3 z3.exe

echo $'\n=================='
echo $'Getting BOOGIE ...'
echo $'==================\n'

cd ${BUILD_ROOT}
git clone https://github.com/boogie-org/boogie.git
cd ${BUILD_ROOT}/boogie
git checkout ${BOOGIE_RELEASE}

echo $'\n==================='
echo $'Building BOOGIE ...'
echo $'===================\n'

xbuild /p:TargetFrameworkProfile="" /p:Configuration=Debug Boogie.sln

echo $'\n=================='
echo $'Getting CORRAL ...'
echo $'==================\n'

cd ${BUILD_ROOT}
git clone https://github.com/pdeligia/corral.git
cd ${BUILD_ROOT}/corral
git checkout ${CORRAL_RELEASE}

echo $'\n==================='
echo $'Building CORRAL ...'
echo $'===================\n'

cd ${BUILD_ROOT}/corral/references
cp ${BUILD_ROOT}/boogie/Binaries/AbsInt.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Basetypes.dll .
cp ${BUILD_ROOT}/boogie/Binaries/CodeContractsExtender.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Core.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Concurrency.dll .
cp ${BUILD_ROOT}/boogie/Binaries/ExecutionEngine.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Graph.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Houdini.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Model.dll .
cp ${BUILD_ROOT}/boogie/Binaries/ParserHelper.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Provers.SMTLib.dll .
cp ${BUILD_ROOT}/boogie/Binaries/VCExpr.dll .
cp ${BUILD_ROOT}/boogie/Binaries/VCGeneration.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Boogie.exe .
cp ${BUILD_ROOT}/boogie/Binaries/BVD.exe .
cp ${BUILD_ROOT}/boogie/Binaries/Doomed.dll .
cp ${BUILD_ROOT}/boogie/Binaries/Predication.dll .
cd ${BUILD_ROOT}/corral
xbuild /p:TargetFrameworkProfile="" /p:Configuration=Debug cba.sln
ln -s ${BUILD_ROOT}/z3/install/bin/z3 ${BUILD_ROOT}/corral/bin/Debug/z3.exe

echo $'\n====================='
echo $'Getting CHAUFFEUR ...'
echo $'=====================\n'

cd ${BUILD_ROOT}
git clone https://github.com/mc-imperial/chauffeur.git ${BUILD_ROOT}/chauffeur/src

echo $'\n======================'
echo $'Building CHAUFFEUR ...'
echo $'======================\n'

mkdir -p ${BUILD_ROOT}/chauffeur/build
cd ${BUILD_ROOT}/chauffeur/build
cmake -D LLVM_CONFIG=${BUILD_ROOT}/llvm_and_clang/build/bin -D CMAKE_BUILD_TYPE=Release ../src
make -j4

echo $'\n================='
echo $'Getting WHOOP ...'
echo $'=================\n'

cd ${BUILD_ROOT}
cp -a ${PROJECT_ROOT}/. ${BUILD_ROOT}/whoop/

echo $'\n=================='
echo $'Building WHOOP ...'
echo $'==================\n'

cd ${BUILD_ROOT}/whoop
xbuild /p:TargetFrameworkProfile="" /p:Configuration=Debug whoop.sln

echo $'\n====================='
echo $'Configuring WHOOP ...'
echo $'=====================\n'

cp /home/vagrant/whoop/whoop/Scripts/Vagrant/findtools.vagrant.py findtools.py

echo $'\n========'
echo $'Done ...'
echo $'========\n'
