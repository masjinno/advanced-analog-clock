@echo off
dotnet publish src\AdvancedAnalogClock\src\AdvancedAnalogClock.App\AdvancedAnalogClock.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:UseAppHost=true -p:DebugType=None -p:DebugSymbols=false -o publish
@echo on
