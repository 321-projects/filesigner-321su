@echo off
setlocal EnableExtensions DisableDelayedExpansion
if /i "%~1"=="/worker" goto build

title 321.su File Signer - Release publish
set "FS_EXIT=1"
set "FS_LOG="
set "FS_PUSHED="
pushd "%~dp0"
if errorlevel 1 goto directory_failed
set "FS_PUSHED=1"
set "FS_LOG=%CD%\publish.log"

echo 321.su File Signer - Release publish
echo Project: "%CD%"
> "%FS_LOG%" echo 321.su File Signer - Release publish
if errorlevel 1 goto log_failed
>> "%FS_LOG%" echo Project: "%CD%"
echo Building Release...
"%SystemRoot%\System32\cmd.exe" /d /c .\publish.bat /worker >> "%FS_LOG%" 2>&1
set "FS_EXIT=%errorlevel%"
if "%FS_EXIT%"=="0" goto published

type "%FS_LOG%"
echo.
echo Publish failed. Exit code: %FS_EXIT%
goto finish

:published
echo Published: "%CD%\publish\FileSigner321.exe"
goto finish

:directory_failed
echo ERROR: Could not open the project directory.
goto finish

:log_failed
echo ERROR: Could not create "%FS_LOG%".
echo Extract the project to a folder you can write to.
set "FS_LOG="

:finish
if defined FS_LOG echo Log: "%FS_LOG%"
if defined FS_PUSHED popd
if /i "%~1"=="/nopause" exit /b %FS_EXIT%
echo.
pause
exit /b %FS_EXIT%

:build
if not exist "FileSigner321.csproj" goto project_missing
set "FS_MSBUILD="
set "FS_VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%FS_VSWHERE%" set "FS_VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%FS_VSWHERE%" goto from_path
set "FS_PROBE=%TEMP%\321su-filesigner-msbuild-%RANDOM%-%RANDOM%.tmp"
"%FS_VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe > "%FS_PROBE%"
if exist "%FS_PROBE%" set /p "FS_MSBUILD="<"%FS_PROBE%"
if exist "%FS_PROBE%" del /q "%FS_PROBE%" >nul 2>&1
if defined FS_MSBUILD if exist "%FS_MSBUILD%" goto compile

:from_path
for %%I in (MSBuild.exe) do set "FS_MSBUILD=%%~$PATH:I"
if defined FS_MSBUILD goto compile
set "FS_MSBUILD=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if exist "%FS_MSBUILD%" goto compile
set "FS_MSBUILD=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
if exist "%FS_MSBUILD%" goto compile
echo ERROR: MSBuild was not found. Install Visual Studio Build Tools and the .NET Framework 4.8 Developer Pack.
exit /b 1

:compile
echo MSBuild: "%FS_MSBUILD%"
"%FS_MSBUILD%" "FileSigner321.csproj" /nologo /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /verbosity:normal
set "FS_BUILD_EXIT=%errorlevel%"
if not "%FS_BUILD_EXIT%"=="0" exit /b %FS_BUILD_EXIT%
if not exist "bin\Release\FileSigner321.exe" goto artifacts_missing
if not exist "publish\" mkdir "publish"
if not exist "publish\" goto copy_failed
if exist "publish\FileSigner321.exe.config" del /q "publish\FileSigner321.exe.config"
if exist "publish\FileSigner321.exe.config" goto old_config_locked
copy /y "bin\Release\FileSigner321.exe" "publish\FileSigner321.exe"
if errorlevel 1 goto copy_failed
echo Published: "%CD%\publish\FileSigner321.exe"
exit /b 0

:project_missing
echo ERROR: FileSigner321.csproj was not found. Extract the entire ZIP before running publish.bat.
exit /b 1

:artifacts_missing
echo ERROR: MSBuild returned success, but the expected EXE is missing.
exit /b 1

:old_config_locked
echo ERROR: Could not remove the obsolete FileSigner321.exe.config from publish.
exit /b 1

:copy_failed
echo ERROR: Could not copy the build to publish. Check folder permissions and close FileSigner321.exe if it is running.
exit /b 1
