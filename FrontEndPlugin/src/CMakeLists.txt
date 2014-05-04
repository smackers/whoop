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
	
  execute_process(
    COMMAND ${LLVM_CONFIG_EXECUTABLE} --ldflags
    OUTPUT_VARIABLE LLVM_LDFLAGS
    OUTPUT_STRIP_TRAILING_WHITESPACE
  )
	
  execute_process(
    COMMAND ${LLVM_CONFIG_EXECUTABLE} --libs
    OUTPUT_VARIABLE LLVM_LIBS
    OUTPUT_STRIP_TRAILING_WHITESPACE
  )
		
	set(LLVM_CXXFLAGS "${LLVM_CXXFLAGS} -fno-rtti")
	set(CXXFLAGS "-I\"${LLVM_SRC}/tools/clang/include\" -I\"${LLVM_BUILD}/tools/clang/include\" ${CXXFLAGS}")
	set(LLVM_LDFLAGS "${LLVM_LDFLAGS} ${LLVM_LIBS}")	
	
	set(CLANG_LIBS -lclangTooling -lclangFrontendTool -lclangFrontend -lclangDriver -lclangSerialization
					-lclangCodeGen -lclangParse -lclangSema -lclangStaticAnalyzerFrontend -lclangStaticAnalyzerCheckers
					-lclangStaticAnalyzerCore -lclangAnalysis -lclangARCMigrate -lclangRewriteFrontend -lclangRewriteCore
					-lclangEdit -lclangAST -lclangLex -lclangBasic ${LLVM_LIBS} -lcurses)
			
else ()
	#   set(LLVM_SRC "" CACHE PATH "LLVM source directory")
	#   set(LLVM_BUILD "" CACHE PATH "LLVM build directory")
	#   set(LLVM_BUILD_TYPE "" CACHE STRING "LLVM build type")
	# 
	#   if (NOT EXISTS "${LLVM_SRC}/include/llvm")
	#     message(FATAL_ERROR "Invalid LLVM source directory: ${LLVM_SRC}")
	#   endif()
	# 
	#   if (NOT EXISTS "${LLVM_BUILD}/lib")
	#     message(FATAL_ERROR "Invalid LLVM build directory: ${LLVM_SRC}")
	#   endif()
	# 
	#   set(LLVM_LIBDIR "${LLVM_BUILD}/lib/${LLVM_BUILD_TYPE}")
	#   if (NOT EXISTS "${LLVM_LIBDIR}")
	#     message(FATAL_ERROR "Invalid LLVM build directory: ${LLVM_BUILD}")
	#   endif()
	# 
	#   set(LLVM_CXXFLAGS "\"/I${LLVM_SRC}/include\" \"/I${LLVM_BUILD}/include\"")
	#   set(LLVM_LDFLAGS "")
	#   set(LLVM_LIBS "${LLVM_LIBDIR}/LLVMAnalysis.lib" "${LLVM_LIBDIR}/LLVMTarget.lib"
	# 							"${LLVM_LIBDIR}/LLVMMC.lib" "${LLVM_LIBDIR}/LLVMObject.lib"
	# 							"${LLVM_LIBDIR}/LLVMCore.lib" "${LLVM_LIBDIR}/LLVMSupport.lib")
	# set(CLANG_CXXFLAGS "${LLVM_CXXFLAGS} \"/I${LLVM_SRC}/tools/clang/include\" \"/I${LLVM_BUILD}/tools/clang/include\"")
endif()

add_executable(chauffeur chauffeur.cpp)

target_link_libraries(chauffeur ${CLANG_LIBS})
target_link_libraries(chauffeur ${LLVM_LDFLAGS})

set_target_properties(chauffeur PROPERTIES COMPILE_FLAGS "${CXXFLAGS} ${LLVM_CXXFLAGS}")

