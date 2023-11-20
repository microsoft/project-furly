@echo off

set connectionString=%~1
docker run -d -it --privileged -p 8883:8883 -p 1883:1883 -e connectionString=%connectionString% iotedge:latest
rem docker exec -it --privileged %ID% iotedge logs edgeHub -f

