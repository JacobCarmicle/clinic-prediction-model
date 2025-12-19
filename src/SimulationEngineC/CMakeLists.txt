cmake_minimum_required(VERSION 3.16)
project(simulation_engine_c C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Build both static and shared libraries
add_library(simengine STATIC
  src/simulation.c
)

add_library(simengine_shared SHARED
  src/simulation.c
)

# Set output name for shared library (platform-specific)
if(APPLE)
  set_target_properties(simengine_shared PROPERTIES OUTPUT_NAME "simulation")
elseif(UNIX)
  set_target_properties(simengine_shared PROPERTIES OUTPUT_NAME "simulation")
elseif(WIN32)
  set_target_properties(simengine_shared PROPERTIES OUTPUT_NAME "simulation")
endif()

target_include_directories(simengine PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/include
  ${CMAKE_CURRENT_SOURCE_DIR}/src
)

target_include_directories(simengine_shared PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/include
  ${CMAKE_CURRENT_SOURCE_DIR}/src
)

add_executable(simcli cli/simcli.c)
target_link_libraries(simcli PRIVATE simengine)

add_executable(test_simulation tests/test_simulation.c)
target_link_libraries(test_simulation PRIVATE simengine)

add_executable(benchmark_simulation tests/benchmark_simulation.c)
target_link_libraries(benchmark_simulation PRIVATE simengine)
