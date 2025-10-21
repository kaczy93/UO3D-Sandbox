#!/bin/bash

glslc -fshader-stage=vertex TerrainVertex.glsl -o TerrainVertex.spv
glslc -fshader-stage=fragment TerrainFragment.glsl -o TerrainFragment.spv
glslc -fshader-stage=compute TerrainCompute1Pos.glsl -o TerrainCompute1Pos.spv
glslc -fshader-stage=compute TerrainCompute2Normals.glsl -o TerrainCompute2Normals.spv
glslc -fshader-stage=compute TerrainCompute3Vertices.glsl -o TerrainCompute3Vertices.spv
