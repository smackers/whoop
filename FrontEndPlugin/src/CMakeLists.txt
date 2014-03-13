# 
# Copyright (c) 2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
# This file is distributed under the MIT License. See LICENSE for details.
# 

cmake_minimum_required(VERSION 2.8)
project(chauffeur)

if (NOT WIN32 OR MSYS OR CYGWIN)
  find_program(LLVM_CONFIG_EXECUTABLE NAMES llvm-config PATHS ${LLVM_CONFIG} NO_DEFAULT_PATH DOC "llvm-config")
	  
	if (LLVM_CONFIG_EXECUTABLE STREQUAL "LLVM_CONFIG_EXECUTABLE-NOTFOUND")
    message(FATAL_ERROR "llvm-config could not be found!")
  endif()
	
  execute_process(
    COMMAND ${LLVM_CONFIG_EXECUTABLE} --src-root
    OUTPUT_VARIABLE LLVM_SRC
    OUTPUT_STRIP_TRAILING_WHITESPACE
  )
	
  execute_process(
    COMMAND ${LLVM_CONFIG_EXECUTABLE} --obj-root
    OUTPUT_VARIABLE LLVM_BUILD
    OUTPUT_STRIP_TRAILING_WHITESPACE
  )
			
  execute_process(
    COMMAND ${LLVM_CONFIG_EXECUTABLE} --cxxflags
    OUTPUT_VARIABLE LLVM_CXXFLAGS
    OUTPUT_STRIP_TRAILING_WHITESPACE
  )
	
	set(LLVM_CXXFLAGS "${LLVM_CXXFLAGS} -fno-exceptions -fno-rtti")
	
  execute_process(
    COMMAND ${LLVM_CONFIG_EXECUTABLE} --libs
    OUTPUT_VARIABLE LLVM_LIBS
    OUTPUT_STRIP_TRAILING_WHITESPACE
  )
	
  execute_process(
    COMMAND ${LLVM_CONFIG_EXECUTABLE} --ldflags
    OUTPUT_VARIABLE LLVM_LDFLAGS
    OUTPUT_STRIP_TRAILING_WHITESPACE
  )
	
	set(PLUGIN_CXXFLAGS "${LLVM_CXXFLAGS} -I\"${LLVM_SRC}/tools/clang/include\" -I\"${LLVM_BUILD}/tools/clang/include\"")
				
  if (${CMAKE_SYSTEM_NAME} MATCHES "Linux")
    set(LLVM_CXXFLAGS "${LLVM_CXXFLAGS} -std=c++0x -stdlib=libc++")
    set(PLUGIN_CXXFLAGS "${PLUGIN_CXXFLAGS} -Wno-strict-aliasing")
  endif()
  
  if (${CMAKE_SYSTEM_NAME} MATCHES "Darwin")
    set(LLVM_CXXFLAGS "${LLVM_CXXFLAGS} -std=c++11 -stdlib=libc++")
    set(PLUGIN_LDFLAGS "-Wl,-undefined,dynamic_lookup")
  endif()
			
else ()
  set(LLVM_SRC "" CACHE PATH "LLVM source directory")
  set(LLVM_BUILD "" CACHE PATH "LLVM build directory")
  set(LLVM_BUILD_TYPE "" CACHE STRING "LLVM build type")

  if (NOT EXISTS "${LLVM_SRC}/include/llvm")
    message(FATAL_ERROR "Invalid LLVM source directory: ${LLVM_SRC}")
  endif()

  set(LLVM_LIBDIR "${LLVM_BUILD}/lib/${LLVM_BUILD_TYPE}")
  if (NOT EXISTS "${LLVM_LIBDIR}")
    message(FATAL_ERROR "Invalid LLVM build directory: ${LLVM_BUILD}")
  endif()

  set(LLVM_CXXFLAGS "\"/I${LLVM_SRC}/include\" \"/I${LLVM_BUILD}/include\"")
  set(LLVM_LDFLAGS "")
  set(LLVM_LIBS "${LLVM_LIBDIR}/LLVMAnalysis.lib" "${LLVM_LIBDIR}/LLVMTarget.lib"
								"${LLVM_LIBDIR}/LLVMMC.lib" "${LLVM_LIBDIR}/LLVMObject.lib"
								"${LLVM_LIBDIR}/LLVMCore.lib" "${LLVM_LIBDIR}/LLVMSupport.lib")
	set(CLANG_CXXFLAGS "${LLVM_CXXFLAGS} \"/I${LLVM_SRC}/tools/clang/include\" \"/I${LLVM_BUILD}/tools/clang/include\"")
endif()

add_library(chauffeur SHARED chauffeur.cpp)

set_target_properties(chauffeur PROPERTIES COMPILE_FLAGS "${PLUGIN_CXXFLAGS}")

target_link_libraries(chauffeur ${PLUGIN_LDFLAGS})
