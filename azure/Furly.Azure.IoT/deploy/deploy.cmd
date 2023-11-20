@REM Copyright (c) Microsoftsoft GbR. All rights reserved.
@REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

@setlocal EnableExtensions EnableDelayedExpansion
@echo off

set current-path=%~dp0
rem // remove trailing slash
set current-path=%current-path:~0,-1%
shift

set PWSH=powershell
call pwsh-setup.cmd
%PWSH% -ExecutionPolicy Unrestricted ./deploy.ps1 %*
popd
