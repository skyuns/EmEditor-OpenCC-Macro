@echo off
:: 必須第一行就切換編碼，否則選單必亂碼
chcp 65001 >nul
setlocal enabledelayedexpansion

:: 1. 偵測系統語系
set "LID="
for /f "tokens=2 delims==" %%a in ('wmic os get locale /value ^| findstr "Locale"') do (
    for /f "tokens=1 delims= " %%b in ("%%a") do set "LID=%%b"
)

:: 2. 初始化語系變數 (預設英文)
set "U_L=ENG"
set "T1=OpenCC Convert Tool"
set "F_SEL=Select file number (0 to Exit): "
set "M_SEL=Select mode number (0 to Back): "
set "EXE=Processing..."
set "SUCC=Converted successfully!"
set "TIME_L=Time:"
set "SEC_L=sec"
set "OUT_L=Output:"
set "DONE=Task finished."
set "NEXT=1. Continue  0. Exit"
set "ERR=Conversion failed."

:: --- 繁體中文 (0404 台灣 / 0c04 香港) ---
if "%LID%"=="0404" set "U_L=CHT"
if "%LID%"=="0c04" set "U_L=CHT"
if "%U_L%"=="CHT" (
    set "T1=OpenCC 檔案轉換工具"
    set "F_SEL=請選擇要轉換的檔案編號 (輸入 0 退出): "
    set "M_SEL=請選擇模式編號 (輸入 0 返回): "
    set "EXE=正在進行轉換..."
    set "SUCC=轉換成功！"
    set "TIME_L=耗時"
    set "SEC_L=秒"
    set "OUT_L=輸出檔案:"
    set "DONE=任務已完成。"
    set "NEXT=1. 繼續轉換其他檔案  0. 結束"
    set "ERR=轉換失敗。"
)

:: --- 簡體中文 (0804) ---
if "%LID%"=="0804" (
    set "U_L=CHS"
    set "T1=OpenCC 文件转换工具"
    set "F_SEL=请选择要转换的文件编号 (输入 0 退出): "
    set "M_SEL=请选择模式编号 (输入 0 返回): "
    set "EXE=正在进行转换..."
    set "SUCC=转换成功！"
    set "TIME_L=耗时"
    set "SEC_L=秒"
    set "OUT_L=输出文件:"
    set "DONE=任务已完成。"
    set "NEXT=1. 继续转换其他文件  0. 结束"
    set "ERR=转换失败。"
)

:: 3. 定義模式說明
if "%U_L%"=="CHT" (
    set "D1=[簡轉繁-標準]" & set "D2=[簡轉台標]" & set "D3=[簡轉台慣]" & set "D4=[簡轉港標]"
    set "D5=[簡轉繁-分詞]" & set "D6=[簡轉台標-分詞]" & set "D7=[簡轉台慣-分詞]" & set "D8=[簡轉港標-分詞]"
    set "D9=[台標轉繁標準]" & set "D10=[港標轉繁標準]" & set "D11=[日轉繁標準]"
    set "D12=[繁轉簡-標準]" & set "D13=[台標轉簡]" & set "D14=[台標轉陸慣]" & set "D15=[港標轉簡]"
    set "D16=[繁標準轉台標]" & set "D17=[繁標準轉港標]" & set "D18=[台標轉陸慣-分詞]" & set "D19=[繁轉日語]"
) else if "%U_L%"=="CHS" (
    set "D1=[简转繁-标准]" & set "D2=[简转台标]" & set "D3=[简转台惯]" & set "D4=[简转港标]"
    set "D5=[简转繁-分词]" & set "D6=[简转台标-分词]" & set "D7=[简转台惯-分词]" & set "D8=[简转港标-分词]"
    set "D9=[台标转繁标准]" & set "D10=[港标转繁标准]" & set "D11=[日转繁标准]"
    set "D12=[繁转简-标准]" & set "D13=[台标转简]" & set "D14=[台标转陆惯]" & set "D15=[港标转简]"
    set "D16=[繁标准转台标]" & set "D17=[繁标准转港标]" & set "D18=[台标转陆惯-分词]" & set "D19=[繁转日语]"
) else (
    set "D1=[S to T]" & set "D2=[S to TW]" & set "D3=[S to TWP]" & set "D4=[S to HK]"
    set "D5=[S2T Jieba]" & set "D6=[S2TW Jieba]" & set "D7=[S2TWP Jieba]" & set "D8=[S2HK Jieba]"
    set "D9=[TW to T]" & set "D10=[HK to T]" & set "D11=[JP to T]"
    set "D12=[T to S]" & set "D13=[TW to S]" & set "D14=[TW to SP]" & set "D15=[HK to S]"
    set "D16=[T to TW]" & set "D17=[T to HK]" & set "D18=[TW2SP Jieba]" & set "D19=[T to JP]"
)

:FILE_MENU
cls
set "fc=" & set "cfg="
echo ======================================================
echo          %T1%
echo ======================================================
echo.
set "count=0"
for %%f in (*.txt) do (
    set /a count+=1
    set "file!count!=%%f"
    echo  !count!. %%f
)
if %count%==0 ( echo No .txt files. & pause & goto END )
echo.
set /p "fc=%F_SEL%"
if "%fc%"=="0" goto END
if not defined file%fc% goto FILE_MENU
set "in=!file%fc%!"

:MODE_MENU
cls
echo Target: %in%
echo ------------------------------------------------------
echo  1. s2t          %D1%     2. s2tw         %D2%
echo  3. s2twp        %D3%        4. s2hk         %D4%
echo  5. s2t_jieba    %D5%     6. s2tw_jieba   %D6%
echo  7. s2twp_jieba  %D7%   8. s2hk_jieba   %D8%
echo  9. tw2t         %D9%   10. hk2t         %D10%
echo 11. jp2t         %D11%
echo.
echo 12. t2s          %D12%    13. tw2s         %D13%
echo 14. tw2sp        %D14%     15. hk2s         %D15%
echo 16. t2tw         %D16%   17. t2hk         %D17%
echo 18. tw2sp_jieba  %D18%
echo.
echo 19. t2jp         %D19%
echo ------------------------------------------------------
echo 0. BACK
echo.
set /p "mc=%M_SEL%"
if "%mc%"=="0" goto FILE_MENU

if "%mc%"=="1"  set "cfg=s2t"
if "%mc%"=="2"  set "cfg=s2tw"
if "%mc%"=="3"  set "cfg=s2twp"
if "%mc%"=="4"  set "cfg=s2hk"
if "%mc%"=="5"  set "cfg=s2t_jieba"
if "%mc%"=="6"  set "cfg=s2tw_jieba"
if "%mc%"=="7"  set "cfg=s2twp_jieba"
if "%mc%"=="8"  set "cfg=s2hk_jieba"
if "%mc%"=="9"  set "cfg=tw2t"
if "%mc%"=="10" set "cfg=hk2t"
if "%mc%"=="11" set "cfg=jp2t"
if "%mc%"=="12" set "cfg=t2s"
if "%mc%"=="13" set "cfg=tw2s"
if "%mc%"=="14" set "cfg=tw2sp"
if "%mc%"=="15" set "cfg=hk2s"
if "%mc%"=="16" set "cfg=t2tw"
if "%mc%"=="17" set "cfg=t2hk"
if "%mc%"=="18" set "cfg=tw2sp_jieba"
if "%mc%"=="19" set "cfg=t2jp"

if not defined cfg goto MODE_MENU

:: 執行轉換
for %%A in ("%in%") do set "base=%%~nA"
set "out=%base%_%cfg%.txt"
if exist "%out%" (
    set /a "suffix=1"
    :DUP_LOOP
    set "out=%base%_%cfg%_!suffix!.txt"
    if exist "!out!" ( set /a "suffix+=1" & goto DUP_LOOP )
)

set "t1=%TIME: =0%"
set "t1=%t1:,=.%"
echo.
echo [EXE] %EXE%
opencc -i "%in%" -o "%out%" -c %cfg%.json

set "t2=%TIME: =0%"
set /a "h1=1%t1:~0,2%-100, m1=1%t1:~3,2%-100, s1=1%t1:~6,2%-100, c1=1%t1:~9,2%-100"
set /a "h2=1%t2:~0,2%-100, m2=1%t2:~3,2%-100, s2=1%t2:~6,2%-100, c2=1%t2:~9,2%-100"
set /a "diff=(h2*360000+m2*6000+s2*100+c2)-(h1*360000+m1*6000+s1*100+c1)"
set /a "sec=diff/100, ms=diff%%100"
if %ms% lss 10 set "ms=0%ms%"

if errorlevel 1 ( echo %ERR% & pause & goto FILE_MENU )

cls
echo ======================================================
echo          %T1% - RESULT
echo ======================================================
echo.
echo %cfg% %SUCC% %TIME_L% %sec%.%ms% %SEC_L%。%OUT_L% %out%
echo.
echo ------------------------------------------------------
set "act="
set /p "act=%NEXT%: "
if "%act%"=="1" goto FILE_MENU

:END
echo.
echo %DONE%
goto :EOF
