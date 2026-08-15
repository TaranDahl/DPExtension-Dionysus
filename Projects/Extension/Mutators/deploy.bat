@echo off
setlocal enabledelayedexpansion

:: 定义源目录和目标目录路径
set "sourceDir=F:\Mental Omega Mods\Mods\hwmjini\Files\DynamicPatcher\Projects\Extension\Mutators"
set "destDir=F:\Mental Omega Mods\Mods\hwmjini\Files\DynamicPatcher\Scripts"
set "buildDir=F:\Mental Omega Mods\Mods\hwmjini\Files\DynamicPatcher\Build"
set "packagesDir=F:\Mental Omega Mods\Mods\hwmjini\Files\DynamicPatcher\Packages"

:: 检查源目录是否存在
if not exist "!sourceDir!" (
    echo 错误：源目录 "!sourceDir!" 不存在
    pause
    exit /b 1
)

:: 创建目标目录（如果不存在）
if not exist "!destDir!" (
    mkdir "!destDir!"
    if errorlevel 1 (
        echo 错误：无法创建目标目录 "!destDir!"
        pause
        exit /b 1
    )
)

:: 复制所有扩展名为.cs且文件名以Script结尾的文件到目标目录
echo 正在复制文件...
copy /y "!sourceDir!\*Script.cs" "!destDir!" >nul 2>&1
if errorlevel 1 (
    echo 警告：未找到符合条件的文件或复制过程出错
) else (
    echo 文件复制完成
)

:: 清空Build目录
echo 正在清空Build目录...
if exist "!buildDir!" (
    del /q /f /s "!buildDir!\*.*" >nul 2>&1
    for /d %%d in ("!buildDir!\*") do rd /s /q "%%d" >nul 2>&1
    echo Build目录清空完成
) else (
    echo 警告：Build目录 "!buildDir!" 不存在
)

:: 清空Packages目录
echo 正在清空Packages目录...
if exist "!packagesDir!" (
    del /q /f /s "!packagesDir!\*.*" >nul 2>&1
    for /d %%d in ("!packagesDir!\*") do rd /s /q "%%d" >nul 2>&1
    echo Packages目录清空完成
) else (
    echo 警告：Packages目录 "!packagesDir!" 不存在
)

echo 所有操作完成
pause