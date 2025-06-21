
dotnet tool install -g Azure.IoT.Operations.ProtocolCompiler --add-source https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json --version 0.10.0-nightly-20250529.1

set namespace=Azure.IoT.Operations.Mock
rmdir /s /q Mock
mkdir Mock
rmdir /s /q %namespace%
mkdir %namespace%
set cmdLine=
set cmdLine=%cmdLine% --outDir %namespace% --serverOnly
mkdir Mock\Common
Azure.Iot.Operations.ProtocolCompiler --modelFile .\dtdl\adr-base-service.json %cmdLine%
mkdir Mock\AdrBaseService
copy %namespace%\AdrBaseService\*.cs Mock\AdrBaseService
copy %namespace%\*.cs Mock\Common
Azure.Iot.Operations.ProtocolCompiler --modelFile .\dtdl\device-discovery-service.json %cmdLine%
mkdir Mock\DeviceDiscoveryService
copy %namespace%\DeviceDiscoveryService\*.cs Mock\DeviceDiscoveryService
copy %namespace%\*.cs Mock\Common
popd
rmdir /s /q %namespace%
rem dotnet tool uninstall -g Azure.IoT.Operations.ProtocolCompiler

