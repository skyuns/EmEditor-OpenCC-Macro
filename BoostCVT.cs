// Project: OpenCC for EmEditor Macro v0.42 BoostCVT
// Author: skyuns (https://github.com/skyuns/EmEditor-OpenCC-Macro 備援: https://gitcode.com/skyuns/EmEditor-OpenCC-Macro)
// Purpose: 多執行緒高速繁簡轉換 BoostCVT 增壓引擎，專為處理 EmEditor 大規模文字設計，支援結巴分詞、語法邏輯與動態詞典載入。
// Note: BoostCVT 加速組件為選配項目，僅在大規模文字且組件存在時啟動。使用者可視需求自由部署，只需 EXE 或 DLL 其中一種即可；使用時須經由 .jsee 腳本驅動，不支援獨立運作。
// EXE 編譯指令 : C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /optimize+ /t:exe /r:System.dll /r:System.Windows.Forms.dll /r:System.Core.dll /out:BoostCVT.exe BoostCVT.cs
// DLL 編譯指令 : C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /optimize+ /t:library /r:System.dll /r:System.Windows.Forms.dll /r:System.Core.dll /out:BoostCVT.dll BoostCVT.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Reflection;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Threading;

[assembly: AssemblyTitle("BoostCVT Engine")]
[assembly: AssemblyDescription("High-performance Text Converter for EmEditor")]
[assembly: AssemblyCompany("skyuns")]
[assembly: AssemblyProduct("BoostCVT")]
[assembly: AssemblyCopyright("Copyright © 2026 skyuns (天匀). All rights reserved.")]
[assembly: AssemblyVersion("0.42.0.0")]
[assembly: AssemblyFileVersion("0.42.0.0")]

namespace MyTools {
    public class TextProcessor {
        class TrieNode { 
            public Dictionary<char, TrieNode> Children; 
            public string Value; 
            public string OriginalKey;
            public double LogFreq = -18.0; // Jieba 分詞對數頻率

            public bool IsVisionAnchor; 
            public bool IsVisionVocab; 
            public bool IsContextAnchor;
        }

        // HMM 模型結構定義 (使用陣列消除雜湊開銷)
        class HmmModel {
            // 索引映射：0:B, 1:M, 2:E, 3:S
            public double[] start_p = new double[4]; 
            public double[,] trans_p = new double[4, 4]; 
            public double[] emit_p = new double[4 * 65536];

            public HmmModel() {
                // 初始化為極小值
                for (int i = 0; i < 4; i++) {
                    start_p[i] = -3.14e100;
                    for (int j = 0; j < 4; j++) {
                        trans_p[i, j] = -3.14e100;
                    }
                    for (int k = 0; k < 65536; k++) {
                        emit_p[i * 65536 + k] = -3.14e100;
                    }
                }
            }
        }

        static HashSet<string> VisionAnchors = new HashSet<string>();
        static HashSet<string> VisionVocabs = new HashSet<string>();
        static HashSet<string> ContextLogicAnchors = new HashSet<string>();

// PhraseLogic 邏輯
class PhraseRule {
    public string Key;
    public string Target;

    public List<string> LeftIncludes = new List<string>();
    public List<string> LeftExcludes = new List<string>();

    public List<string> RightIncludes = new List<string>();
    public List<string> RightExcludes = new List<string>();
}

static Dictionary<char, List<PhraseRule>> FastPhraseRules = new Dictionary<char, List<PhraseRule>>();
static bool[] HasPhraseLogicStart = new bool[65536];

static void LoadPhraseLogic(string source) {
    Match m = Regex.Match(source, @"const\s+PhraseLogic\s*=\s*\{(.*?)\};", RegexOptions.Singleline);
    if (!m.Success) return;
    var matches = Regex.Matches(m.Groups[1].Value, @"""(?<k>[^""]+)"":\s*""(?<v>[^""]+)""");

    foreach (Match entry in matches) {
        string k = entry.Groups["k"].Value;
        string v = entry.Groups["v"].Value;

        PhraseRule rule = new PhraseRule();
        rule.Key = k;

        var parts = v.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        var targets = parts[0].Split(' ');
        rule.Target = targets.Length > 1 ? targets[1] : targets[0];

        // 解析左權重
        if (parts.Length > 1) {
            foreach (var tag in parts[1].Split('|')) {
                if (string.IsNullOrEmpty(tag)) continue;
                if (tag[0] == '!') rule.LeftExcludes.Add(tag.Substring(1));
                else rule.LeftIncludes.Add(tag);
            }
        }
        // 解析右權重
        if (parts.Length > 2) {
            foreach (var tag in parts[2].Split('|')) {
                if (string.IsNullOrEmpty(tag)) continue;
                if (tag[0] == '!') rule.RightExcludes.Add(tag.Substring(1));
                else rule.RightIncludes.Add(tag);
            }
        }

        if (k.Length > 0) {
            char firstChar = k[0];
            HasPhraseLogicStart[firstChar] = true;
            if (!FastPhraseRules.ContainsKey(firstChar)) {
                FastPhraseRules[firstChar] = new List<PhraseRule>();
            }
            FastPhraseRules[firstChar].Add(rule);
        }
    }
}

static bool TryApplyPhraseLogic(int i, int start, int len, string input, ref int localReplaceCount, out string matchedTarget, out int matchLen) {
    matchedTarget = null;
    matchLen = 0;

    char firstChar = input[start + i];

    if (!HasPhraseLogicStart[firstChar]) return false;

    List<PhraseRule> rules = FastPhraseRules[firstChar];
    int absoluteIdx = start + i;

    for (int rIdx = 0; rIdx < rules.Count; rIdx++) {
        PhraseRule rule = rules[rIdx];
        string pKey = rule.Key;

        if (i + pKey.Length <= len) {
            bool keyMatch = true;
            for (int k = 0; k < pKey.Length; k++) {
                if (input[absoluteIdx + k] != pKey[k]) { keyMatch = false; break; }
            }
            if (!keyMatch) continue;

            // 定義視野邊界
            int leftStart = Math.Max(0, absoluteIdx - 6);
            int leftLen = absoluteIdx - leftStart;
            int rightStart = absoluteIdx + pKey.Length;
            int rightLen = Math.Min(start + len, rightStart + 6) - rightStart;

            bool isTriggered = false;

            // 向左比對
            if (rule.LeftExcludes.Count > 0) {
                bool hasExclude = false;
                foreach (var tag in rule.LeftExcludes) {
                    if (IntrospectiveContains(input, leftStart, leftLen, tag)) { hasExclude = true; break; }
                }
                if (hasExclude) continue;
            }

            if (rule.LeftIncludes.Count > 0) {
                foreach (var tag in rule.LeftIncludes) {
                    if (IntrospectiveContains(input, leftStart, leftLen, tag)) { isTriggered = true; break; }
                }
            }

            // 向右比對
            if (rule.RightExcludes.Count > 0) {
                bool hasExclude = false;
                foreach (var tag in rule.RightExcludes) {
                    if (IntrospectiveContains(input, rightStart, rightLen, tag)) { hasExclude = true; break; }
                }
                if (hasExclude) continue; 
            }

            if (rule.RightIncludes.Count > 0) {
                foreach (var tag in rule.RightIncludes) {
                    if (IntrospectiveContains(input, rightStart, rightLen, tag)) { isTriggered = true; break; }
                }
            }

            if ((rule.LeftIncludes.Count == 0 && rule.RightIncludes.Count == 0) || isTriggered) {
                localReplaceCount++;
                matchedTarget = rule.Target;
                matchLen = pKey.Length;
                return true;
            }
        }
    }
    return false;
}

        static bool IntrospectiveContains(string src, int viewStart, int viewLen, string target) {
            if (target.Length == 0) return true;
            if (viewLen < target.Length) return false;

            int maxIdx = viewStart + viewLen - target.Length;
            for (int s = viewStart; s <= maxIdx; s++) {
                bool match = true;
                for (int t = 0; t < target.Length; t++) {
                    if (src[s + t] != target[t]) {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        static Func<int, int, string, string> LogicDelegate = null;
        static object LogicInstance = null;
        static bool[] HasContextLogic = new bool[65536];

        // 執行緒專屬記憶體池 (Zero-Allocation 核心)
        class ThreadState {
            public double[] routeScore = new double[1024];
            public int[] routeNext = new int[1024];
            public double[] vBuf = new double[4096];
            public int[] bpBuf = new int[4096];
            public int[] spBuf = new int[1024];
            public StringBuilder sb = new StringBuilder(1024);
            public StringBuilder fbSb = new StringBuilder(128);

            public void EnsureSize(int len) {
                if (routeScore.Length < len + 1) {
                    int newSize = len + 4096;
                    routeScore = new double[newSize];
                    routeNext = new int[newSize];
                    sb.Capacity = newSize;
                }
            }

            public void EnsureViterbiSize(int obsLen) {
                if (obsLen * 4 > vBuf.Length) {
                    int newSize = obsLen + 512;
                    vBuf = new double[newSize * 4];
                    bpBuf = new int[newSize * 4];
                    spBuf = new int[newSize];
                }
            }
        }

        [STAThread]
        public static void Main(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("Usage: BoostCVT.exe <macroPath> <configStr>");
                return;
            }
            int replaceCount = Convert(args[0], args[1]);
            Environment.Exit(replaceCount);
        }

        public static int Convert(string macroPathOrCmd, string configStrOrMode) {
            // 設定優先權
                try {
                    if (Process.GetCurrentProcess().PriorityClass != ProcessPriorityClass.High) {
                        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                    }
                } catch {
                }

            try {
                // 🌟 [讀取詞典模式] 讀取外部詞典
                if (macroPathOrCmd == "LOAD_DICT") {
                    string[] dictParams = configStrOrMode.Split('|');
                    string dictMode = dictParams[0];
                    string jseePath = dictParams.Length > 1 ? dictParams[1] : "";
                    bool needJieba = dictParams.Length > 2 && dictParams[2] == "1";
                    bool phraseExpEnabled = dictParams.Length > 3 ? dictParams[3] == "1" : true;
                    bool charactersExt = dictParams.Length > 4 && dictParams[4] == "1";

                    DateTime dictJseeDate = File.Exists(jseePath) ? File.GetLastWriteTime(jseePath) : DateTime.MinValue;

                    string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string targetDictDir = Path.Combine(exeDir, "dictionary");

                    // 加入 fTWVariantsPhrases 定義
                    string fPhrase = "", fChar = "", fTWVariants = "", fPhraseExp = "", fException = "";
                    string fTWVariantsPhrases = "TWVariantsPhrases.txt"; 

                    // 根據模式指派詞典的優先權與替補機制
                    string fJieba = "";
                    if (needJieba) {
                        string pBig = Path.Combine(targetDictDir, "dict.txt.big");
                        string pSmall = Path.Combine(targetDictDir, "jieba.dict.utf8");

                        if (dictMode == "S2T" || dictMode == "S2TE") {
                            fJieba = File.Exists(pSmall) ? "jieba.dict.utf8" : (File.Exists(pBig) ? "dict.txt.big" : "");
                        } else {
                            fJieba = File.Exists(pBig) ? "dict.txt.big" : (File.Exists(pSmall) ? "jieba.dict.utf8" : "");
                        }
                    }

                    string fUserDict = needJieba ? "user.dict.utf8" : "";
                    string fHmm = needJieba ? "hmm_model.utf8" : "";

                    if (dictMode == "T2S") { fPhrase = "TSPhrases.txt"; fChar = "TSCharacters.txt"; fTWVariants = "TWVariants.txt"; fPhraseExp = "T2SrawPhraseExpandedData.txt"; fException = "T2SrawExceptionData.txt"; fTWVariantsPhrases = ""; }
                    else if (dictMode == "S2T") { fPhrase = "STPhrases.txt"; fChar = "STCharacters.txt"; fTWVariants = "TWVariants.txt"; fPhraseExp = "S2TrawPhraseExpandedData.txt"; fException = "S2TrawExceptionData.txt"; }
                    else if (dictMode == "S2TE") { fPhrase = "STPhrases.txt"; fChar = "STCharacters.txt"; fTWVariants = "TWVariants.txt"; fPhraseExp = "S2TErawPhraseExpandedData.txt"; fException = "S2TrawExceptionData.txt"; }
                    else if (dictMode == "S2TWP") { fPhrase = "TWPhrases.txt"; fPhraseExp = "S2TWPrawPhraseExpandedData.txt"; fTWVariantsPhrases = ""; }
                    else if (dictMode == "TW2SP") { fPhrase = "TWPhrasesRev.txt"; fPhraseExp = "TW2SPrawPhraseExpandedData.txt"; fTWVariantsPhrases = ""; }

                    bool hasNewerUpdate = false;

                    string[] filesToCheck = { fPhrase, fChar, fTWVariants, fPhraseExp, fException, fJieba, fUserDict, fHmm, fTWVariantsPhrases };
                    foreach (var f in filesToCheck) {
                        if (!string.IsNullOrEmpty(f)) {
                            string txtPath = Path.Combine(targetDictDir, f);
                            if (File.Exists(txtPath) && File.GetLastWriteTime(txtPath) > dictJseeDate) {
                                hasNewerUpdate = true;
                                break;
                            }
                        }
                    }

                    // 讀取邏輯：即使沒有更新，如果需要 Jieba 且檔案存在，也須讀取
                    bool mustReadJieba = needJieba && File.Exists(Path.Combine(targetDictDir, fJieba));
                    bool mustReadUser = needJieba && File.Exists(Path.Combine(targetDictDir, fUserDict));
                    bool mustReadHmm = needJieba && File.Exists(Path.Combine(targetDictDir, fHmm));

                    if (!hasNewerUpdate && !mustReadJieba) return 0;

                    string sourceJsee = File.Exists(jseePath) ? File.ReadAllText(jseePath, Encoding.UTF8) : "";

                    Func<string, string, bool, string> LoadData = (blockName, fileName, force) => {
                        if (string.IsNullOrEmpty(fileName)) return "";
                        string txtPath = Path.Combine(targetDictDir, fileName);
                        if (!File.Exists(txtPath)) return "";

                        if ((hasNewerUpdate || force) && File.GetLastWriteTime(txtPath) > dictJseeDate || force) {
                            return File.ReadAllText(txtPath, Encoding.UTF8);
                        }
                        if (!hasNewerUpdate && !force) return "";
                        if (!string.IsNullOrEmpty(sourceJsee)) return ExtractBlock(sourceJsee, blockName);
                        return "";
                    };

                    string phraseData = LoadData("rawPhraseData", fPhrase, false);
                    string charData = LoadData("rawCharData", fChar, false);
                    string twVariants = LoadData("rawTWVariants", fTWVariants, false);
                    string phraseExpData = phraseExpEnabled ? LoadData("rawPhraseExpandedData", fPhraseExp, false) : "";
                    string exceptionData = LoadData("rawExceptionData", fException, false);
                    string jiebaData = LoadData("rawJiebaData", fJieba, mustReadJieba); 
                    string userDictData = LoadData("rawUserDictData", fUserDict, mustReadUser);
                    string hmmData = LoadData("rawHmmData", fHmm, mustReadHmm);
                    // 讀取 rawTWVariantsPhrases
                    string twVariantsPhrasesData = LoadData("rawTWVariantsPhrases", fTWVariantsPhrases, false);

                    // 若 charactersExt 為 true 且單字對應表有內容，執行 C# 行級高效過濾
                    if (charactersExt && !string.IsNullOrEmpty(charData)) {
                        StringBuilder cleanCharSb = new StringBuilder(charData.Length);
                        bool lastLineWasTofuRisk = false;

                        using (StringReader sr = new StringReader(charData)) {
                            string l;
                            while ((l = sr.ReadLine()) != null) {
                                string trimL = l.Trim();
                                if (trimL.StartsWith("# @tofu-risk:")) {
                                    lastLineWasTofuRisk = true;
                                    cleanCharSb.AppendLine(l);
                                    continue;
                                }
                                if (trimL.StartsWith("#")) {
                                    lastLineWasTofuRisk = false;
                                    cleanCharSb.AppendLine(l);
                                    continue;
                                }

                                string[] pts = l.Split('\t');
                                if (lastLineWasTofuRisk && pts.Length >= 2) {
                                    string key = pts[0].Trim();
                                    string[] vals = pts[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                    if (vals.Length > 0 && vals[0].Trim() == key) {
                                        string[] newVals = new string[vals.Length - 1];
                                        Array.Copy(vals, 1, newVals, 0, vals.Length - 1);
                                        vals = newVals;
                                    }

                                    if (vals.Length > 0) {
                                        cleanCharSb.AppendLine(key + "\t" + string.Join(" ", vals));
                                    }
                                } else {
                                    cleanCharSb.AppendLine(l);
                                }
                                lastLineWasTofuRisk = false;
                            }
                        }
                        charData = cleanCharSb.ToString();
                    }

                    string output = string.Join("|||BLOCK_SEP|||", new string[] { phraseData, charData, twVariants, phraseExpData, exceptionData, jiebaData, userDictData, hmmData, twVariantsPhrasesData });

                    try { Clipboard.SetText(output); } catch { return 0; }
                    return 1;
                }

                // 🌟 [常規模式] 大檔轉換的核心邏輯
                string macroPath = macroPathOrCmd;
                string configStr = configStrOrMode;
                string[] parts = configStr.Split('|');

                if (parts.Length < 7) {
                    MessageBox.Show("Engine Version Mismatch!", "BoostCVT Engine", MessageBoxButtons.OK);
                    Environment.Exit(-1);
                }

                string mode = parts[0];
                bool isCtx = parts[1] == "1";
                bool isVis = parts[2] == "1";
                bool isShift = parts[3] == "1";
                bool isAlt = parts[4] == "1";
                bool isExtDict = (parts[5] == "1");

                // Jieba 參數解析
                bool isJiebaActive = parts.Length > 6 && parts[6] == "1";
                int hmmLimit = 50;
                if (parts.Length > 7) int.TryParse(parts[7], out hmmLimit);

                int threadCount = 4;
                if (parts.Length > 8) int.TryParse(parts[8], out threadCount);
                else if (parts.Length > 6 && parts.Length < 8) int.TryParse(parts[6], out threadCount);

                bool isPhraseExpActive = parts.Length > 9 ? parts[9] == "1" : true;
                bool isCharactersExtActive = parts.Length > 10 && parts[10] == "1";

                bool isS2T = mode.IndexOf("S2T") != -1;
                bool isT2S = mode.IndexOf("T2S") != -1;

                if (!File.Exists(macroPath)) {
                    MessageBox.Show("Macro file not found:\n" + macroPath, "BoostCVT Engine - File Error", MessageBoxButtons.OK);
                    Environment.Exit(-1);
                }

                string source = File.ReadAllText(macroPath, Encoding.UTF8);
                string baseDir = Path.GetDirectoryName(macroPath);
                DateTime jseeDate = File.GetLastWriteTime(macroPath);

                // 加入 fileTWVariantsPhrases 定義與阻斷機制
                string filePhrase = "", fileChar = "", fileTWVariants = "", filePhraseExp = "", fileException = "";
                string fileTWVariantsPhrases = "TWVariantsPhrases.txt";

                // 根據簡繁模式進行詞典的優先與替補判定
                string fileJieba = "";
                if (isJiebaActive) {
                    string pBig = Path.Combine(baseDir, "dictionary", "dict.txt.big");
                    string pSmall = Path.Combine(baseDir, "dictionary", "jieba.dict.utf8");

                    if (mode == "S2T" || mode == "S2TE") {
                        fileJieba = File.Exists(pSmall) ? "jieba.dict.utf8" : (File.Exists(pBig) ? "dict.txt.big" : "");
                    } else {
                        fileJieba = File.Exists(pBig) ? "dict.txt.big" : (File.Exists(pSmall) ? "jieba.dict.utf8" : "");
                    }
                }

                string fileUserDict = isJiebaActive ? "user.dict.utf8" : "";
                string fileHmm = isJiebaActive ? "hmm_model.utf8" : "";

                if (mode == "T2S") { filePhrase = "TSPhrases.txt"; fileChar = "TSCharacters.txt"; fileTWVariants = "TWVariants.txt"; filePhraseExp = "T2SrawPhraseExpandedData.txt"; fileException = "T2SrawExceptionData.txt"; fileTWVariantsPhrases = ""; }
                else if (mode == "S2T") { filePhrase = "STPhrases.txt"; fileChar = "STCharacters.txt"; fileTWVariants = "TWVariants.txt"; filePhraseExp = "S2TrawPhraseExpandedData.txt"; fileException = "S2TrawExceptionData.txt"; }
                else if (mode == "S2TE") { filePhrase = "STPhrases.txt"; fileChar = "STCharacters.txt"; fileTWVariants = "TWVariants.txt"; filePhraseExp = "S2TErawPhraseExpandedData.txt"; fileException = "S2TrawExceptionData.txt"; }
                else if (mode == "S2TWP") { filePhrase = "TWPhrases.txt"; filePhraseExp = "S2TWPrawPhraseExpandedData.txt"; fileTWVariantsPhrases = ""; }
                else if (mode == "TW2SP") { filePhrase = "TWPhrasesRev.txt"; filePhraseExp = "TW2SPrawPhraseExpandedData.txt"; fileTWVariantsPhrases = ""; }

                Func<string, string, bool, string> LoadSmartBlock = (blockName, fileName, forceJieba) => {
                    if (string.IsNullOrEmpty(fileName)) return "";
                    string txtPath = Path.Combine(baseDir, "dictionary", fileName);

                    // 結巴相關詞典，只要檔案存在就讀取
                    if (forceJieba && File.Exists(txtPath)) {
                        return File.ReadAllText(txtPath, Encoding.UTF8);
                    }

                    // 原本的 OpenCC 邏輯維持日期檢查
                    if (isExtDict && File.Exists(txtPath) && File.GetLastWriteTime(txtPath) > jseeDate) {
                        return File.ReadAllText(txtPath, Encoding.UTF8);
                    }
                    return ExtractBlock(source, blockName);
                };

                string rawTWVariantsStr = LoadSmartBlock("rawTWVariants", fileTWVariants, false);
                string rawExceptionDataStr = LoadSmartBlock("rawExceptionData", fileException, false);
                string rawPhraseExpandedDataStr = isPhraseExpActive ? LoadSmartBlock("rawPhraseExpandedData", filePhraseExp, false) : "";
                string rawPhraseDataStr = LoadSmartBlock("rawPhraseData", filePhrase, false);
                string rawCharDataStr = LoadSmartBlock("rawCharData", fileChar, false);
                string rawJiebaDataStr = LoadSmartBlock("rawJiebaData", fileJieba, isJiebaActive);
                string rawUserDictDataStr = LoadSmartBlock("rawUserDictData", fileUserDict, isJiebaActive);
                string rawHmmDataStr = LoadSmartBlock("rawHmmData", fileHmm, isJiebaActive);
                string rawTWVariantsPhrasesStr = LoadSmartBlock("rawTWVariantsPhrases", fileTWVariantsPhrases, false);

                LoadPhraseLogic(source);

                if (string.IsNullOrEmpty(rawPhraseDataStr) && string.IsNullOrEmpty(rawCharDataStr) && (isS2T || isT2S)) {
                    MessageBox.Show("Dictionary data missing.", "BoostCVT", MessageBoxButtons.OK);
                    Environment.Exit(-1);
                }

                VisionAnchors.Clear(); VisionVocabs.Clear(); ContextLogicAnchors.Clear();
                LogicDelegate = null; Array.Clear(HasContextLogic, 0, HasContextLogic.Length);

                // 結巴有開，就載入 Vision 陣列作為加扣分依據
                if ((isVis || isJiebaActive) && !isShift) {
                    VisionAnchors = ExtractSet(source, "VisionAnchors");
                    VisionVocabs = ExtractSet(source, "VisionVocabs");
                }

                if (isCtx && !isShift) {
                    ContextLogicAnchors = ExtractSet(source, "ContextLogicAnchors");
                    CompileContextLogic(source, mode);
                }

                var finalDict = new Dictionary<string, string>();
                var exceptionMap = new Dictionary<string, string>();
                var variantMap = new Dictionary<char, char>();

                var twPhrasesMap = new Dictionary<string, string>();
                var reverseCharMap = new Dictionary<char, string>();

                if (!isShift && !isAlt && (isS2T || isT2S)) {
                    using (StringReader r = new StringReader(rawTWVariantsStr)) {
                        string l; while ((l = r.ReadLine()) != null) {
                            if (string.IsNullOrWhiteSpace(l) || l[0] == '#') continue;
                            string[] p = l.Split('\t');
                            if (p.Length >= 2 && p[0].Length > 0 && p[1].Length > 0) {
                                string k = p[0].Trim(); string v = p[1].Split(' ')[0].Trim();
                                if (k.Length > 0 && v.Length > 0) {
                                    if (isS2T) variantMap[k[0]] = v[0];
                                    else variantMap[v[0]] = k[0];
                                }
                            }
                        }
                    }
                }

                if (isS2T && !isAlt && !isShift && !string.IsNullOrEmpty(rawTWVariantsPhrasesStr)) {
                    using (StringReader r = new StringReader(rawTWVariantsPhrasesStr)) {
                        string l; while ((l = r.ReadLine()) != null) {
                            if (string.IsNullOrWhiteSpace(l) || l[0] == '#') continue;
                            string[] p = l.Split('\t');
                            if (p.Length >= 2 && p[0].Length > 0 && p[1].Length > 0) {
                                string k = p[0].Trim(); 
                                string v = p[1].Split(' ')[0].Trim();
                                if (k.Length > 0 && v.Length > 0) twPhrasesMap[k] = v;
                            }
                        }
                    }
                }

                Func<string, string> applyTWVariants = (str) => {
                    if (!isS2T || isAlt || isShift) return str;
                    StringBuilder sb = new StringBuilder(str.Length);
                    foreach (char c in str) sb.Append(variantMap.ContainsKey(c) ? variantMap[c] : c);
                    return sb.ToString();
                };

                if (!isShift) {
                    using (StringReader r = new StringReader(rawExceptionDataStr)) {
                        string l; while ((l = r.ReadLine()) != null) {
                            if (string.IsNullOrWhiteSpace(l) || l[0] == '#') continue;
                            string[] p = l.Split('\t');
                            if (p.Length >= 2) {
                                string k = p[0].Trim(); string v = p[1].Trim();
                                exceptionMap[k] = v;
                                exceptionMap[v] = v;
                            }
                        }
                    }
                }

                Action<string, bool> parseMainData = (data, isCharFile) => {
                    if (string.IsNullOrEmpty(data)) return;
                    using (StringReader r = new StringReader(data)) {
                        string l; 
                        bool lastLineWasTofuRisk = false;

                        while ((l = r.ReadLine()) != null) {
                            string trimL = l.Trim();
                            if (isCharFile && isCharactersExtActive) {
                                if (trimL.StartsWith("# @tofu-risk:")) {
                                    lastLineWasTofuRisk = true;
                                    continue;
                                }
                                if (trimL.StartsWith("#")) {
                                    lastLineWasTofuRisk = false;
                                    continue;
                                }
                            } else {
                                if (string.IsNullOrWhiteSpace(l) || l[0] == '#') continue;
                            }

                            string[] pts = l.Split('\t');
                            if (pts.Length >= 2) {
                                string key = pts[0].Trim();
                                if (isCtx && !isShift && ContextLogicAnchors.Contains(key)) {
                                    lastLineWasTofuRisk = false;
                                    continue;
                                }

                                string valPart = pts[1];
                                // 如果是字元表且觸發豆腐字擴充模式
                                if (isCharFile && isCharactersExtActive && lastLineWasTofuRisk) {
                                    string[] vals = valPart.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (vals.Length > 0 && vals[0].Trim() == key) {
                                        string[] newVals = new string[vals.Length - 1];
                                        Array.Copy(vals, 1, newVals, 0, vals.Length - 1);
                                        vals = newVals;
                                    }
                                    valPart = string.Join(" ", vals);
                                }

                                if (string.IsNullOrEmpty(valPart)) {
                                    lastLineWasTofuRisk = false;
                                    continue;
                                }

                                string firstTarget = valPart.Split(' ')[0].Trim();

                                string target = exceptionMap.ContainsKey(key) ? exceptionMap[key] : (twPhrasesMap.ContainsKey(firstTarget) ? twPhrasesMap[firstTarget] : applyTWVariants(firstTarget));
                                if (!finalDict.ContainsKey(key)) finalDict[key] = target;

                                if (isCharFile && key.Length == 1 && valPart.Length > 0) {
                                    string[] tVals = valPart.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (string v in tVals) {
                                        if (v.Length > 0) {
                                            char tChar = v[0];
                                            if (!reverseCharMap.ContainsKey(tChar)) {
                                                reverseCharMap[tChar] = key;
                                            }
                                        }
                                    }
                                }
                            }
                            lastLineWasTofuRisk = false;
                        }
                    }
                };

                if (!isShift) {
                    parseMainData(rawExceptionDataStr, false);
                    parseMainData(rawPhraseExpandedDataStr, false);
                }
                parseMainData(rawPhraseDataStr, false);
                if (mode.IndexOf("S2TWP") == -1 && mode.IndexOf("TW2SP") == -1) {
                    parseMainData(rawCharDataStr, true);
                }

                if (isS2T && !isAlt && !isShift && twPhrasesMap.Count > 0 && reverseCharMap.Count > 0) {
                    foreach (var kvp in twPhrasesMap) {
                        string twKey = kvp.Key;
                        string twVal = kvp.Value;
                        StringBuilder scKeySb = new StringBuilder(twKey.Length);

                        for (int i = 0; i < twKey.Length; i++) {
                            char tcChar = twKey[i];
                            // 原字形精確反查
                            if (reverseCharMap.ContainsKey(tcChar)) {
                                scKeySb.Append(reverseCharMap[tcChar]);
                            } else {
                                // 嘗試降維再查
                                char normChar = variantMap.ContainsKey(tcChar) ? variantMap[tcChar] : tcChar;
                                scKeySb.Append(reverseCharMap.ContainsKey(normChar) ? reverseCharMap[normChar] : normChar.ToString());
                            }
                        }

                        string scKey = scKeySb.ToString();
                        if (!finalDict.ContainsKey(scKey)) {
                            finalDict[scKey] = twVal;
                        }
                    }
                }

                if (isS2T && !isAlt && !isShift) {
                    foreach (var kvp in variantMap) {
                        string vK = kvp.Key.ToString();
                        if (!finalDict.ContainsKey(vK)) finalDict[vK] = kvp.Value.ToString();
                    }
                } else if (isT2S && !isAlt && !isShift) {
                    foreach (var kvp in variantMap) {
                        string twChar = kvp.Key.ToString(); string genericChar = kvp.Value.ToString();
                        if (!finalDict.ContainsKey(twChar)) {
                            if (finalDict.ContainsKey(genericChar)) finalDict[twChar] = finalDict[genericChar];
                            else finalDict[twChar] = genericChar;
                        }
                    }
                }

                TrieNode[] rootNodes = new TrieNode[65536];
                foreach (var kvp in finalDict) AddWord(rootNodes, kvp.Key, kvp.Value);

                // 建立錨點開頭索引 (Bitset)，並將標記直接注入 Trie 樹，實踐零分配
                bool[] IsAnchorStart = new bool[65536];
                foreach (string s in VisionAnchors) if (!string.IsNullOrEmpty(s)) { IsAnchorStart[s[0]] = true; AddWord(rootNodes, s, null, -18.0, 1); }
                foreach (string s in VisionVocabs) if (!string.IsNullOrEmpty(s)) { IsAnchorStart[s[0]] = true; AddWord(rootNodes, s, null, -18.0, 2); }
                foreach (string s in ContextLogicAnchors) if (!string.IsNullOrEmpty(s)) { IsAnchorStart[s[0]] = true; AddWord(rootNodes, s, null, -18.0, 4); }

                // 檢查結巴詞典字串是否存在，若不存在則關閉旗標
                if (isJiebaActive && string.IsNullOrEmpty(rawJiebaDataStr)) {
                    isJiebaActive = false;
                }

                // 解析並載入 Jieba 詞頻
                if (isJiebaActive && !string.IsNullOrEmpty(rawJiebaDataStr)) {
                    double logTotal = Math.Log(34732707.0); // 標準 N=3473萬
                    using (StringReader r = new StringReader(rawJiebaDataStr)) {
                        string l; while ((l = r.ReadLine()) != null) {
                            if (string.IsNullOrWhiteSpace(l) || l[0] == '#') continue;
                            int space1 = l.IndexOf(' ');
                            if (space1 > 0) {
                                string word = l.Substring(0, space1);
                                int space2 = l.IndexOf(' ', space1 + 1);
                                string freqStr = space2 == -1 ? l.Substring(space1 + 1) : l.Substring(space1 + 1, space2 - space1 - 1);

                                double freq;
                                if (double.TryParse(freqStr, out freq)) {
                                    AddWord(rootNodes, word, null, Math.Log(freq) - logTotal);
                                }
                            }
                        }
                    }

                    // 載入 UserDict
                    if (!string.IsNullOrEmpty(rawUserDictDataStr)) {
                        using (StringReader r = new StringReader(rawUserDictDataStr)) {
                            string l; while ((l = r.ReadLine()) != null) {
                                l = l.Replace("\r", "");
                                if (string.IsNullOrWhiteSpace(l) || l[0] == '#') continue;
                                string[] pts = l.Split(' ');
                                if (pts.Length > 0 && !string.IsNullOrEmpty(pts[0])) {
                                    string word = pts[0];
                                    double freq = 5000; // 預設高頻
                                    if (pts.Length >= 2) {
                                        double parsedFreq;
                                        if (double.TryParse(pts[1], out parsedFreq)) {
                                            freq = parsedFreq;
                                        }
                                    }
                                    AddWord(rootNodes, word, null, Math.Log(freq) - logTotal);
                                }
                            }
                        }
                    }
                }

                // 載入 HMM 模型參數 (二維陣列映射)
                HmmModel hmm = null;
                if (isJiebaActive && !string.IsNullOrEmpty(rawHmmDataStr)) {
                    try {
                        hmm = new HmmModel();
                        string[] lines = rawHmmDataStr.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

                        // 定義內部映射表確保狀態與陣列索引一致
                        // 映射規則：B:0, M:1, E:2, S:3
                        Dictionary<char, int> sIdx = new Dictionary<char, int> { { 'B', 0 }, { 'M', 1 }, { 'E', 2 }, { 'S', 3 } };
                        // 檔案讀取順序為 B, E, M, S
                        char[] fileStates = { 'B', 'E', 'M', 'S' };

                        for (int i = 0; i < lines.Length; i++) {
                            string line = lines[i].Trim();
                            if (string.IsNullOrEmpty(line)) continue;

                            if (line == "#prob_start" && i + 1 < lines.Length) {
                                string[] vals = lines[i + 1].Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                for (int j = 0; j < 4 && j < vals.Length; j++) {
                                    double val; 
                                    if (double.TryParse(vals[j], out val)) hmm.start_p[sIdx[fileStates[j]]] = val;
                                }
                            }
                            else if (line == "#prob_trans 4x4 matrix") {
                                for (int j = 0; j < 4; j++) {
                                    if (i + 1 + j >= lines.Length) break;
                                    string[] row = lines[i + 1 + j].Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                    for (int k = 0; k < 4 && k < row.Length; k++) {
                                        double val; 
                                        if (double.TryParse(row[k], out val)) 
                                            hmm.trans_p[sIdx[fileStates[j]], sIdx[fileStates[k]]] = val;
                                    }
                                }
                            }
                            else if (line == "#prob_emit 4 lines") {
                                for (int j = 0; j < 4; j++) {
                                    char stateChar = fileStates[j];
                                    int sRow = sIdx[stateChar];
                                    int foundIdx = -1;
                                    for (int k = i; k < lines.Length; k++) {
                                        if (lines[k].Trim() == "#" + stateChar) { foundIdx = k; break; }
                                    }
                                    if (foundIdx != -1 && foundIdx + 1 < lines.Length) {
                                        string[] pairs = lines[foundIdx + 1].Split(',');
                                        foreach (string pair in pairs) {
                                            string[] kv = pair.Split(':');
                                            if (kv.Length == 2 && kv[0].Length > 0) {
                                                double val;
                                                if (double.TryParse(kv[1], out val)) 
                                                    hmm.emit_p[sRow * 65536 + (int)kv[0][0]] = val;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    } catch {
                        hmm = null;
                    }
                }

                string fullInput = Clipboard.GetText();
                if (string.IsNullOrEmpty(fullInput)) return 0;
                int totalLen = fullInput.Length;

                // 微型斷句隔離 (Micro-Chunking) 
                int targetChunkSize = 65536; 
                int fastSkipLimit = (mode == "S2TWP" || mode == "TW2SP") ? 0x21 : 0x80;
                List<int> cutsList = new List<int>();
                cutsList.Add(0);
                int expectedPos = 0;

                while (expectedPos + targetChunkSize < totalLen) {
                    int safeCut = fullInput.IndexOf('\n', expectedPos + targetChunkSize);

                    if (safeCut != -1 && safeCut - expectedPos < targetChunkSize * 4) {
                        cutsList.Add(safeCut + 1);
                        expectedPos = safeCut + 1;
                    } else {
                        int fallbackLimit = Math.Min(totalLen - 1, expectedPos + targetChunkSize + 1024);
                        int fallbackCut = -1;
                        for (int c = expectedPos + targetChunkSize; c <= fallbackLimit; c++) {
                            if (fullInput[c] < fastSkipLimit) {
                                fallbackCut = c;
                                break;
                            }
                        }
                        if (fallbackCut != -1) {
                            cutsList.Add(fallbackCut + 1);
                            expectedPos = fallbackCut + 1;
                        } else {
                            cutsList.Add(expectedPos + targetChunkSize);
                            expectedPos += targetChunkSize;
                        }
                    }
                }
                cutsList.Add(totalLen);
                int totalChunks = cutsList.Count - 1;

                string[] chunksOut = new string[totalChunks];
                int totalReplaceCount = 0;

                // 配置加分與懲罰常數 (權重控制面板)
                double DICT_BONUS_2 = 3.0;
                double DICT_BONUS_3 = 7.0;
                double DICT_BONUS_4 = 17.0;
                double PENALTY_ONE_TO_MANY = -7.0;

                // 結巴詞頻分層加分 (8階梯，基於 N=6000萬門檻)
                double FREQ_BONUS_GODLY = 11.0;
                double FREQ_BONUS_LEGENDARY = 9.0;
                double FREQ_BONUS_EPIC = 7.0;
                double FREQ_BONUS_ELITE = 5.0;
                double FREQ_BONUS_HIGH = 3.5;
                double FREQ_BONUS_MID = 2.0;
                double FREQ_BONUS_LOW = 0.5;
                double FREQ_BONUS_FEW = 0.1;

                string ONE_TO_MANY_S2T = "㐹万丑个丰了于云亘仆仇仑价仿伙余佛佣俊修借僵克党冬冲凄准凌几凶出划别刮制勋千升卜占卤卷厂历厘参发只台叶叹吁吃合吊同后向周咨咸咽哄哗唇啮喂噪回团困坛坝埙复夫夸奸姜娘娴宁它家尝尸尽局岩岳巨布帘席干并幸广庵弥弦当录彩征御志念恤恶愈愿戚才扎托扣折抵拐挂挨挽据搜摆斗旋昆暗曲术朱朴杆杠杯杰松板极柜栗核梁欲毁汇沈沾泛注涂涌淀游滟漓炼烟熏玩璇症皂矩确私秋种筑签系纤绱绷胄背胜胡脏腊腌膻致舍艳芸苏苔苹范荐荡荫药获蒙蔑虫蚝蜡蝎表袅裥证谥谷赝赞跖辟迹适郁酸采里鉴针钟钥钫钻铲链锫镋镎镢镰闲雕面须饥鹇洒虱湿袜";
                string ONE_TO_MANY_T2S = "乾儘剋劃噁噹夥崙廬彷徵戰擣於瀋瀰牴畫瞭祇綵線薹藉蘋衹襬覆託諫諮譾買鉅鍊鍾鏇钁開閒阪靦韝願餘餬餱餵驄鵰麪麴麵麼麽齧";

                bool[] IsOneToMany = new bool[65536];
                string targetOneToManyList = (mode == "S2T" || mode == "S2TE") ? ONE_TO_MANY_S2T : ONE_TO_MANY_T2S;
                foreach (char c in targetOneToManyList) {
                    IsOneToMany[c] = true;
                }

                System.Threading.Tasks.ParallelOptions options = new System.Threading.Tasks.ParallelOptions { 
                    MaxDegreeOfParallelism = threadCount 
                };

                System.Threading.Tasks.Parallel.For<ThreadState>(
                    0, 
                    totalChunks, 
                    options, 
                    () => new ThreadState(), 
                    (int tIdx, System.Threading.Tasks.ParallelLoopState loopState, ThreadState state) => {
                        int start = cutsList[tIdx];
                        int end = cutsList[tIdx + 1];
                        int len = end - start;
                        if (len <= 0) { chunksOut[tIdx] = ""; return state; }

                        state.EnsureSize(len);
                        int i = 0, lastMatchEnd = 0, localReplaceCount = 0;
                        bool hasReplacements = false;

                        double[] routeScore = state.routeScore;
                        int[] routeNext = state.routeNext;
                        StringBuilder sb = state.sb;
                        StringBuilder fbSb = state.fbSb;
                        sb.Length = 0;

                        if (isJiebaActive) {
                            // 第 1 階段：Jieba DAG + DP 路由計算
                            routeScore[len] = 0;
                            routeNext[len] = len;

                            Action<int, int> runViterbi = (startPtr, obsLen) => {
                                if (hmm == null || obsLen == 0) {
                                    routeNext[startPtr] = startPtr + obsLen;
                                    return;
                                }
                                state.EnsureViterbiSize(obsLen);
                                double[] V = state.vBuf;
                                int[] backPath = state.bpBuf;
                                int[] statesPath = state.spBuf;

                                int firstCharCode = (int)fullInput[start + startPtr];
                                for (int s = 0; s < 4; s++) {
                                    V[s] = hmm.start_p[s] + hmm.emit_p[s * 65536 + firstCharCode];
                                }

                                for (int t = 1; t < obsLen; t++) {
                                    int charCode = (int)fullInput[start + startPtr + t];
                                    for (int y = 0; y < 4; y++) {
                                        double maxProb = double.NegativeInfinity;
                                        int bestPrevState = 0;
                                        double emitP = hmm.emit_p[y * 65536 + charCode];

                                        for (int y0 = 0; y0 < 4; y0++) {
                                            double prob = V[(t - 1) * 4 + y0] + hmm.trans_p[y0, y] + emitP;
                                            if (prob > maxProb) {
                                                maxProb = prob;
                                                bestPrevState = y0;
                                            }
                                        }
                                        V[t * 4 + y] = maxProb;
                                        backPath[t * 4 + y] = bestPrevState;
                                    }
                                }

                                int lastState = 3; 
                                if (V[(obsLen - 1) * 4 + 2] >= V[(obsLen - 1) * 4 + 3]) lastState = 2;

                                statesPath[obsLen - 1] = lastState;
                                for (int t = obsLen - 2; t >= 0; t--) {
                                    statesPath[t] = backPath[(t + 1) * 4 + statesPath[t + 1]];
                                }

                                int begin = 0;
                                for (int k = 0; k < obsLen; k++) {
                                    int s = statesPath[k];
                                    if (s == 2 || s == 3) {
                                        int hwLen = k - begin + 1;
                                        routeNext[startPtr + begin] = startPtr + begin + hwLen;
                                        begin = k + 1;
                                    }
                                }
                                if (begin < obsLen) {
                                    routeNext[startPtr + begin] = startPtr + obsLen;
                                }
                            };

                            for (int idx = len - 1; idx >= 0; idx--) {
                                char firstChar = fullInput[start + idx];
                                if (firstChar < fastSkipLimit) {
                                    routeScore[idx] = routeScore[idx + 1];
                                    routeNext[idx] = idx + 1;
                                    // 區塊跳躍 (反向單步)
                                    while (idx - 1 >= 0 && fullInput[start + idx - 1] < fastSkipLimit) {
                                        idx--;
                                        routeScore[idx] = routeScore[idx + 1];
                                        routeNext[idx] = idx + 1;
                                    }
                                    continue;
                                }

                                TrieNode node = rootNodes[firstChar];
                                double bestScore = double.NegativeInfinity;
                                int bestNext = idx + 1;

                                if (node == null) {
                                    routeScore[idx] = -18.0 + routeScore[idx + 1];
                                    routeNext[idx] = idx + 1;
                                    continue;
                                }

                                // 動態加扣分常數
                                double PENALTY_VISION_2 = -2.5; // VisionAnchors 2字詞扣分
                                double PENALTY_VISION_3 = -0.5; // VisionAnchors 3字詞(含以上)扣分
                                double PENALTY_CTX = -0.5; // ContextLogicAnchors 扣分
                                double BONUS_VISION_VOCAB = 15.0; // VisionVocabs 額外加分

                                int j = idx;
                                TrieNode curr = node;
                                while (j < len) {
                                    if (curr.LogFreq != -18.0 || curr.Value != null || j == idx) {
                                        double wordFreq = curr.LogFreq;
                                        int wordLen = j - idx + 1;

                                        // A：詞典加分機制
                                        double dictBonus = 0;
                                        if (wordLen > 1 && curr.Value != null) {
                                            if (wordLen >= 4) dictBonus = DICT_BONUS_4;
                                            else if (wordLen == 3) dictBonus = DICT_BONUS_3;
                                            else dictBonus = DICT_BONUS_2;
                                        }

                                        // A-2：結巴詞頻分層加分 (8階梯) + 長度給分
                                        double freqBonus = 0;
                                        if (wordLen > 1 && curr.LogFreq != -18.0) {
                                            if (wordFreq > -9.2) freqBonus = FREQ_BONUS_GODLY;
                                            else if (wordFreq > -9.9) freqBonus = FREQ_BONUS_LEGENDARY;
                                            else if (wordFreq > -10.3) freqBonus = FREQ_BONUS_EPIC;
                                            else if (wordFreq > -11.0) freqBonus = FREQ_BONUS_ELITE;
                                            else if (wordFreq > -13.3) freqBonus = FREQ_BONUS_HIGH;
                                            else if (wordFreq > -15.5) freqBonus = FREQ_BONUS_MID;
                                            else if (wordFreq > -16.5) freqBonus = FREQ_BONUS_LOW;
                                            else freqBonus = FREQ_BONUS_FEW;

                                            if (wordLen >= 4) freqBonus += 9.0;
                                            else if (wordLen == 3) freqBonus += 3.0;
                                        }

                                        // B：一對多單字懲罰機制 (直接定址優化)
                                        double penalty = 0;
                                        if (wordLen == 1 && IsOneToMany[fullInput[start + idx]]) {
                                            penalty = PENALTY_ONE_TO_MANY;
                                        }

                                        // C: 針對 Set 進行精確加扣分
                                        double extraWeight = 0;
                                        if (wordLen > 1) {
                                            if (curr.IsVisionAnchor) {
                                                extraWeight += (wordLen == 2) ? PENALTY_VISION_2 : PENALTY_VISION_3;
                                            }
                                            if (curr.IsContextAnchor) {
                                                extraWeight += PENALTY_CTX;
                                            }
                                            if (curr.IsVisionVocab) {
                                                extraWeight += BONUS_VISION_VOCAB;
                                            }
                                        }

                                        if (j == idx && wordFreq == -18.0) wordFreq = -18.0;

                                        // 總分計算
                                        double score = wordFreq + dictBonus + freqBonus + penalty + extraWeight + routeScore[j + 1];
                                        if (score > bestScore) {
                                            bestScore = score;
                                            bestNext = j + 1;
                                        }
                                    }
                                    j++;
                                    if (j < len) {
                                        TrieNode nxtNode = null;
                                        curr = (curr.Children != null && curr.Children.TryGetValue(fullInput[start + j], out nxtNode)) ? nxtNode : null;
                                        if (curr == null) break;
                                    }
                                }
                                routeScore[idx] = bestScore;
                                routeNext[idx] = bestNext;
                            }

                            // 第 1.5 階段：HMM 處理連續單字
                            int ptr = 0;
                            while (ptr < len) {
                                if (fullInput[start + ptr] < fastSkipLimit) {
                                    // 區塊跳躍 (正向 4 步)
                                    while (ptr + 3 < len && (fullInput[start + ptr] | fullInput[start + ptr + 1] | fullInput[start + ptr + 2] | fullInput[start + ptr + 3]) < fastSkipLimit) ptr += 4;
                                    while (ptr < len && fullInput[start + ptr] < fastSkipLimit) ptr++;
                                    continue;
                                }

                                int nextPtr = routeNext[ptr];
                                int wLen = nextPtr - ptr;
                                char code = fullInput[start + ptr];

                                if (wLen == 1 && code >= 0x4E00 && code <= 0x9FFF) {
                                    int startPtr = ptr;
                                    int endPtr = nextPtr;
                                    while (endPtr < len) {
                                        int nxt = routeNext[endPtr];

                                        char endChar = fullInput[start + endPtr];
                                        if (nxt - endPtr == 1 && endChar >= 0x4E00 && endChar <= 0x9FFF) {
                                            endPtr = nxt;
                                        } else {
                                            break;
                                        }
                                    }

                                    if (endPtr - startPtr > 1) {
                                        if (endPtr - startPtr <= hmmLimit) {
                                            runViterbi(startPtr, endPtr - startPtr);
                                        }
                                    }
                                    ptr = endPtr;
                                } else {
                                    ptr = nextPtr;
                                }
                            }

                            // 第 2 階段：轉換輸出 (整合 ContextLogic 與 Fallback)
                            int extractIdx = 0;
                            while (extractIdx < len) {
                                if (fullInput[start + extractIdx] < fastSkipLimit) {
                                    // 區塊跳躍 (正向 4 步)
                                    while (extractIdx + 3 < len && (fullInput[start + extractIdx] | fullInput[start + extractIdx + 1] | fullInput[start + extractIdx + 2] | fullInput[start + extractIdx + 3]) < fastSkipLimit) extractIdx += 4;
                                    while (extractIdx < len && fullInput[start + extractIdx] < fastSkipLimit) extractIdx++;
                                    continue;
                                }

                                int nxt = routeNext[extractIdx];

                                // 檢查 PhraseLogic
                                if ((nxt - extractIdx) < 4 && isCtx && !isShift && HasPhraseLogicStart[fullInput[start + extractIdx]]) {
                                    string pTarget; int pLen;
                                    if (TryApplyPhraseLogic(extractIdx, start, len, fullInput, ref localReplaceCount, out pTarget, out pLen)) {
                                        hasReplacements = true;
                                        if (extractIdx > lastMatchEnd) sb.Append(fullInput, start + lastMatchEnd, extractIdx - lastMatchEnd);
                                        sb.Append(pTarget);
                                        extractIdx += pLen;
                                        lastMatchEnd = extractIdx;
                                        continue;
                                    }
                                }

                                // 🗡️ 真・視界邏輯介入 (零分配加速)
                                int _wLen = nxt - extractIdx;

                                // 用 IsAnchorStart 擋掉不是錨點的詞
                                if (VisionAnchors != null && VisionAnchors.Count > 0 && _wLen > 1 && IsAnchorStart[fullInput[start + extractIdx]]) 
                                {
                                    // 第一段拔刀：偵測到定錨點 (純字典樹判定，0 Allocation)
                                    bool isAnchor = false;
                                    TrieNode aNode = rootNodes[fullInput[start + extractIdx]];
                                    if (aNode != null) {
                                        for (int k = 1; k < _wLen; k++) {
                                            TrieNode nextN = null;
                                            if (aNode.Children != null && aNode.Children.TryGetValue(fullInput[start + extractIdx + k], out nextN)) {
                                                aNode = nextN;
                                            } else { 
                                                aNode = null; 
                                                break; 
                                            }
                                        }
                                        if (aNode != null && aNode.IsVisionAnchor) isAnchor = true;
                                    }

                                    if (isAnchor)
                                    {
                                        int visionIdx = extractIdx + _wLen - 1; // 站位在交界字
                                        int visionWordLen = 0;

                                        // 向後掃描尋找強勢詞
                                        int vk = visionIdx + 1;
                                        TrieNode continuousNode = rootNodes[fullInput[start + visionIdx]];

                                        while (vk < len && (vk - visionIdx) <= 8)
                                        {
                                            if (continuousNode != null) 
                                            {
                                                TrieNode nextN = null;
                                                if (continuousNode.Children != null && continuousNode.Children.TryGetValue(fullInput[start + vk], out nextN)) {
                                                    continuousNode = nextN;
                                                } else {
                                                    continuousNode = null;
                                                }
                                            }

                                            int currentSubLen = vk - visionIdx + 1;

                                            // 只要長度大於 1，且符合條件即鎖定
                                            if (currentSubLen > 1 && continuousNode != null) 
                                            {
                                                if (continuousNode.Value != null || continuousNode.IsVisionVocab || continuousNode.IsVisionAnchor) 
                                                {
                                                    visionWordLen = currentSubLen; 
                                                }
                                            }

                                            // 核心斷鏈優化
                                            if (continuousNode == null) break;

                                            vk++;
                                        }

                                        // 第二段揮斬：穩定性檢查
                                        if (visionWordLen > 1) 
                                        {
                                            bool isStable = true;
                                            int stabIdx = visionIdx + visionWordLen - 1;

                                            // 再次向後掃描：看有沒有更強的詞
                                            int sk = stabIdx + 1;
                                            TrieNode sNode = rootNodes[fullInput[start + stabIdx]];

                                            while (sk < len && (sk - stabIdx) <= 8)
                                            {
                                                if (sNode != null) 
                                                {
                                                    TrieNode nextSN = null;
                                                    if (sNode.Children != null && sNode.Children.TryGetValue(fullInput[start + sk], out nextSN)) 
                                                    {
                                                        sNode = nextSN;
                                                        // 發現假象，收刀不砍
                                                        if (sNode.IsVisionVocab) 
                                                        {
                                                            isStable = false; 
                                                            break;
                                                        }
                                                    } 
                                                    else 
                                                    {
                                                        sNode = null;
                                                    }
                                                }

                                                // 斷鏈優化
                                                if (sNode == null) break;

                                                sk++;
                                            }

                                            if (isStable)
                                            {
                                                nxt = extractIdx + _wLen - 1;
                                            }
                                        }
                                    }
                                }

                                int wordLen = nxt - extractIdx;
                                char firstChar = fullInput[start + extractIdx];

                                string openccTarget = null;
                                TrieNode node = rootNodes[firstChar];

                                if (node != null) {
                                    if (wordLen == 1) {
                                        if (isCtx && !isShift && LogicDelegate != null && HasContextLogic[firstChar]) {
                                            string logicResult = LogicDelegate((int)firstChar, start + extractIdx, fullInput);
                                            if (logicResult != null) openccTarget = logicResult;
                                        }
                                        if (openccTarget == null && node.Value != null) {
                                            openccTarget = node.Value;
                                        }
                                    } else {
                                        TrieNode currNode = node;
                                        for (int k = 1; k < wordLen; k++) {
                                            TrieNode nextN;
                                            if (currNode.Children != null && currNode.Children.TryGetValue(fullInput[start + extractIdx + k], out nextN)) {
                                                currNode = nextN;
                                            } else {
                                                currNode = null; break;
                                            }
                                        }
                                        if (currNode != null && currNode.Value != null) openccTarget = currNode.Value;
                                    }
                                }

                                // 次級貪婪匹配回退 (Fallback) 與 零分配優化
                                string finalTarget = null;
                                if (openccTarget != null) {
                                    finalTarget = openccTarget;
                                } else {
                                    int subK = 0;
                                    fbSb.Length = 0; // 零分配

                                    while (subK < wordLen) {
                                        char ch = fullInput[start + extractIdx + subK];
                                        int longestMatchLen = 0;
                                        string longestMatchTarget = null;

                                        TrieNode fNode = rootNodes[ch];
                                        if (fNode != null) {
                                            if (fNode.Value != null) {
                                                longestMatchLen = 1;
                                                longestMatchTarget = fNode.Value;
                                            }
                                            int scan = 1;
                                            TrieNode temp = fNode;
                                            while (subK + scan < wordLen) {
                                                TrieNode nextN;
                                                if (temp.Children == null || !temp.Children.TryGetValue(fullInput[start + extractIdx + subK + scan], out nextN)) break;
                                                temp = nextN;
                                                if (temp.Value != null) {
                                                    longestMatchLen = scan + 1;
                                                    longestMatchTarget = temp.Value;
                                                }
                                                scan++;
                                            }
                                        }

                                        if (longestMatchLen <= 1 && isCtx && !isShift && LogicDelegate != null && HasContextLogic[ch]) {
                                            string lRes = LogicDelegate((int)ch, start + extractIdx + subK, fullInput);
                                            if (lRes != null) {
                                                longestMatchTarget = lRes;
                                                longestMatchLen = 1;
                                            }
                                        }

                                        int matchLen = Math.Max(1, longestMatchLen);

                                        if (longestMatchTarget != null) {
                                            if (fbSb.Length == 0 && subK > 0) fbSb.Append(fullInput, start + extractIdx, subK);
                                            fbSb.Append(longestMatchTarget);
                                        } else if (fbSb.Length > 0) {
                                            fbSb.Append(ch);
                                        }

                                        subK += matchLen;
                                    }
                                    if (fbSb.Length > 0) finalTarget = fbSb.ToString();
                                }

                                // 零分配比較邏輯
                                if (finalTarget != null) {
                                    bool isChanged = false;
                                    if (finalTarget.Length != wordLen) isChanged = true;
                                    else { 
                                        for (int k = 0; k < wordLen; k++) {
                                            if (finalTarget[k] != fullInput[start + extractIdx + k]) { isChanged = true; break; } 
                                        } 
                                    }

                                    if (isChanged) {
                                        hasReplacements = true;
                                        localReplaceCount++;
                                        if (extractIdx > lastMatchEnd) sb.Append(fullInput, start + lastMatchEnd, extractIdx - lastMatchEnd);
                                        sb.Append(finalTarget);
                                        lastMatchEnd = nxt;
                                    }
                                }
                                extractIdx = nxt;
                            }

                        } else {
                            // 原本的貪婪最長匹配轉換 (非結巴模式)
                            while (i < len) {
                                while (i + 3 < len && (fullInput[start + i] | fullInput[start + i + 1] | fullInput[start + i + 2] | fullInput[start + i + 3]) < fastSkipLimit) i += 4;
                                while (i < len && fullInput[start + i] < fastSkipLimit) i++; if (i >= len) break; 
                                char firstChar = fullInput[start + i];

                                TrieNode node = rootNodes[firstChar];
                                int longestMatchLen = 0; string longestMatchTarget = null;

                                if (node != null) {
                                    if (node.Value != null) { longestMatchLen = 1; longestMatchTarget = node.Value; }
                                    int j = i + 1; TrieNode curr = node;
                                    while (j < len) {
                                        TrieNode nextNode;
                                        if (curr.Children == null || !curr.Children.TryGetValue(fullInput[start + j], out nextNode)) break;
                                        curr = nextNode;
                                        if (curr.Value != null) { longestMatchLen = j - i + 1; longestMatchTarget = curr.Value; }
                                        j++;
                                    }
                                }

                                // 檢查 PhraseLogic
                                if (longestMatchLen < 4 && isCtx && !isShift && HasPhraseLogicStart[firstChar]) {
                                    string pTarget; int pLen;
                                    if (TryApplyPhraseLogic(i, start, len, fullInput, ref localReplaceCount, out pTarget, out pLen)) {
                                        hasReplacements = true;
                                        if (i > lastMatchEnd) sb.Append(fullInput, start + lastMatchEnd, i - lastMatchEnd);
                                        sb.Append(pTarget);
                                        i += pLen;
                                        lastMatchEnd = i;
                                        continue;
                                    }
                                }

                                // 🔭 視界邏輯 (0 Allocation 極速版)
                                if (isVis && !isShift && longestMatchLen > 1) {
                                    int foundALen = 0;
                                    TrieNode aN = rootNodes[fullInput[start + i]];
                                    if (aN != null) {
                                        if (aN.IsVisionAnchor) foundALen = 1;
                                        // 往前探勘
                                        for (int k = 1; k < longestMatchLen; k++) {
                                            if (aN.Children != null && aN.Children.TryGetValue(fullInput[start + i + k], out aN)) {
                                                if (aN.IsVisionAnchor) foundALen = k + 1;
                                            } else break;
                                        }
                                    }

                                    if (foundALen > 1 && longestMatchLen >= 4 && longestMatchLen > foundALen) foundALen = 0;

                                    if (foundALen > 1) {
                                        int vIdx = i + foundALen - 1; 
                                        int vWordLen = 0; // 改紀錄長度，不產生 String
                                        int vk = vIdx + 1; 
                                        TrieNode vN = rootNodes[fullInput[start + vIdx]];

                                        while (vk < len) {
                                            if (vN != null && vN.Children != null) {
                                                TrieNode nextVN;
                                                if (vN.Children.TryGetValue(fullInput[start + vk], out nextVN)) vN = nextVN; else vN = null;
                                            } else vN = null;

                                            int currentSubLen = vk - vIdx + 1;
                                            // 直接檢查節點屬性，不再呼叫 VisionVocabs.Contains(sub)
                                            if (vN != null && ((vN.Value != null) || vN.IsVisionVocab)) vWordLen = currentSubLen; 

                                            if (vN == null && currentSubLen > 6) break;
                                            vk++;
                                        }

                                        if (vWordLen > 0) {
                                            bool stable = true; 
                                            int dIdx = vIdx + vWordLen - 1; 
                                            int dk = dIdx + 1; 
                                            TrieNode dN = rootNodes[fullInput[start + dIdx]];

                                            while (dk < len) {
                                                if (dN != null && dN.Children != null) {
                                                    TrieNode nextDN;
                                                    if (dN.Children.TryGetValue(fullInput[start + dk], out nextDN)) dN = nextDN; else dN = null;
                                                } else dN = null;

                                                int currentSubLen = dk - dIdx + 1;

                                                if (dN != null && ((dN.Value != null) || dN.IsVisionVocab)) { stable = false; break; } 

                                                if (dN == null && currentSubLen > 6) break;
                                                dk++;
                                            }

                                            if (stable) {
                                                int bLen = 1; 
                                                TrieNode bN = rootNodes[fullInput[start + i]];
                                                if (bN != null) {
                                                    // 只找尋小於 foundALen - 1 的次長 Anchor
                                                    for (int k = 1; k < foundALen - 1; k++) {
                                                        if (bN.Children != null && bN.Children.TryGetValue(fullInput[start + i + k], out bN)) {
                                                            if (bN.IsVisionAnchor) bLen = k + 1;
                                                        } else break;
                                                    }
                                                }

                                                longestMatchLen = bLen < 2 ? 1 : bLen;

                                                // 重新定位 Target
                                                TrieNode tN = rootNodes[fullInput[start + i]]; 
                                                for (int k = 1; k < longestMatchLen; k++) {
                                                    if (tN.Children != null && tN.Children.TryGetValue(fullInput[start + i + k], out tN)) {} else { tN = null; break; }
                                                }
                                                longestMatchTarget = tN != null ? tN.Value : null;
                                            }
                                        }
                                    }
                                }

                                // ⚓ 語法邏輯 (精簡變數與極速判定版)
                                if (isCtx && !isShift && longestMatchLen <= 1 && LogicDelegate != null && HasContextLogic[firstChar]) {
                                    string lRes = LogicDelegate((int)firstChar, start + i, fullInput);
                                    if (lRes != null) { 
                                        longestMatchTarget = lRes; 
                                        longestMatchLen = 1; 
                                    }
                                }

                                if (longestMatchTarget != null) {
                                    // 原生判斷
                                    int matchLen = longestMatchLen < 1 ? 1 : longestMatchLen;
                                    int targetLen = longestMatchTarget.Length;
                                    bool isChanged = false;

                                    // 展開單字元比對
                                    if (targetLen != matchLen) {
                                        isChanged = true;
                                    } else if (targetLen == 1) {
                                        isChanged = longestMatchTarget[0] != fullInput[start + i];
                                    } else {
                                        // 只有長度大於 1 的詞彙，才會進入迴圈
                                        for (int k = 0; k < targetLen; k++) {
                                            if (longestMatchTarget[k] != fullInput[start + i + k]) { 
                                                isChanged = true; 
                                                break; 
                                            }
                                        }
                                    }

                                    if (isChanged) {
                                        hasReplacements = true;
                                        localReplaceCount++;
                                        if (i > lastMatchEnd) sb.Append(fullInput, start + lastMatchEnd, i - lastMatchEnd);
                                        sb.Append(longestMatchTarget);
                                        lastMatchEnd = i + matchLen;
                                    } 
                                    i += matchLen;
                                } else {
                                    i++;
                                }
                            }
                        }

                        if (!hasReplacements) {
                            chunksOut[tIdx] = fullInput.Substring(start, len);
                        } else {
                            if (lastMatchEnd < len) sb.Append(fullInput, start + lastMatchEnd, len - lastMatchEnd);
                            chunksOut[tIdx] = sb.ToString();
                        }
                        System.Threading.Interlocked.Add(ref totalReplaceCount, localReplaceCount);

                        return state; 
                    },

                    (ThreadState state) => {
                        if (state != null) {
                            if (state.sb != null) state.sb.Length = 0;
                            if (state.fbSb != null) state.fbSb.Length = 0;
                        }
                    }
                );

                Clipboard.SetText(string.Join("", chunksOut));
                return totalReplaceCount;

            } catch (Exception ex) { 
                if (macroPathOrCmd != "LOAD_DICT") {
                    MessageBox.Show("Execution Error: " + ex.Message + "\n" + ex.StackTrace); 
                }
                Environment.Exit(-1);
                return 0; 
            }
        }

        static void CompileContextLogic(string jsSource, string mode) {
            string logicName = mode.IndexOf("T2S") != -1 ? "ContextLogic_T2S" : "ContextLogic";
            var logicMatch = Regex.Match(jsSource, @"const " + logicName + @"\s*=\s*\{(.*?)\};", RegexOptions.Singleline);
            if (!logicMatch.Success) return;

            string mVal = Regex.Match(jsSource, @"const m\s*=\s*(?:""|')(?<v>.*?)(?:""|')").Groups["v"].Value;
            string sn2Val = Regex.Match(jsSource, @"const sn2\s*=\s*(?:""|')(?<v>.*?)(?:""|')").Groups["v"].Value;
            string safeM = mVal.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeSn2 = sn2Val.Replace("\\", "\\\\").Replace("\"", "\\\"");

            StringBuilder cases = new StringBuilder();
            MatchCollection funcMatches = Regex.Matches(logicMatch.Groups[1].Value, @"0x(?<id>[0-9A-Fa-f]+):\s*function\s*\([^)]*\)\s*\{");

            foreach (Match fm in funcMatches) {
                string idStr = fm.Groups["id"].Value;
                int startIdx = fm.Index + fm.Length;
                int depth = 1, endIdx = startIdx;
                bool inStr = false; char qChar = '\0';

                while (endIdx < logicMatch.Groups[1].Value.Length) {
                    char c = logicMatch.Groups[1].Value[endIdx];
                    if (inStr) {
                        if (c == '\\' && endIdx + 1 < logicMatch.Groups[1].Value.Length) endIdx++;
                        else if (c == qChar) inStr = false;
                    } else {
                        if (c == '"' || c == '\'') { inStr = true; qChar = c; }
                        else if (c == '{') depth++;
                        else if (c == '}') { depth--; if (depth == 0) break; }
                    }
                    endIdx++;
                }

                string body = logicMatch.Groups[1].Value.Substring(startIdx, endIdx - startIdx);
                body = Regex.Replace(body, @"//.*?\n", "\n");
                body = body.Replace("\r", " ").Replace("\n", " ");

                body = body.Replace("n===\"色\"==-1", "n!=\"色\""); 
                body = Regex.Replace(body, @"t\.charAt\(([^)]+)\)\s*\|\|\s*[""']\s?[""']", "C(t, $1)");
                body = Regex.Replace(body, @"t\.charAt\(([^)]+)\)", "C(t, $1)");
                body = body.Replace("===", "==").Replace("!==", "!=");
                body = Regex.Replace(body, @"\.indexOf\(([^)]+)\)", ".IndexOf($1, StringComparison.Ordinal)");
                body = body.Replace("var ", "string ");
                cases.AppendLine(string.Format("case 0x{0}: {{ {1} break; }}", idStr, body));
                HasContextLogic[System.Convert.ToInt32(idStr, 16)] = true;
            }
            // 零分配記憶體快取
            string code = "using System;\npublic class DynamicLogic {\n" +
                "  static string[] cCache = new string[65536];\n" +
                "  static DynamicLogic() { for(int i=0; i<65536; i++) cCache[i] = ((char)i).ToString(); }\n" +
                "  string m = \"" + safeM + "\";\n" +
                "  string sn2 = \"" + safeSn2 + "\";\n" +
                "  string C(string t, int i) { return (i >= 0 && i < t.Length) ? cCache[t[i]] : \" \"; }\n" +
                "  public string Run(int c, int i, string t) {\n" +
                "    switch(c) {\n" + cases.ToString() + "    }\n" +
                "    return null;\n  }\n}";

            CompilerParameters cp = new CompilerParameters();
            cp.GenerateInMemory = true;
            cp.ReferencedAssemblies.Add("System.dll");
            CompilerResults cr = new CSharpCodeProvider().CompileAssemblyFromSource(cp, code);

            if (cr.Errors.HasErrors) throw new Exception("ContextLogic Compile Error: " + cr.Errors[0].ErrorText);

            LogicInstance = cr.CompiledAssembly.CreateInstance("DynamicLogic");
            MethodInfo mi = cr.CompiledAssembly.GetType("DynamicLogic").GetMethod("Run");
            LogicDelegate = (Func<int, int, string, string>)Delegate.CreateDelegate(typeof(Func<int, int, string, string>), LogicInstance, mi);
        }

        static void AddWord(TrieNode[] roots, string k, string v, double logFreq = -18.0, byte flag = 0) {
            if (string.IsNullOrEmpty(k)) return;
            char f = k[0]; if (roots[f] == null) roots[f] = new TrieNode();
            TrieNode c = roots[f];
            for (int i = 1; i < k.Length; i++) {
                if (c.Children == null) c.Children = new Dictionary<char, TrieNode>();
                if (!c.Children.ContainsKey(k[i])) c.Children[k[i]] = new TrieNode();
                c = c.Children[k[i]];
            } 
            if (v != null) {
                c.Value = v;
                c.OriginalKey = k; // 紀錄原始詞條
            }
            if (logFreq != -18.0) c.LogFreq = logFreq; // 只更新非預設值的權重

            if ((flag & 1) != 0) c.IsVisionAnchor = true;
            if ((flag & 2) != 0) c.IsVisionVocab = true;
            if ((flag & 4) != 0) c.IsContextAnchor = true;
        }

        static string ExtractBlock(string src, string name) { 
            Match m = Regex.Match(src, name + @"\s*=\s*[`](?<c>[\s\S]*?)[`]", RegexOptions.Singleline); 
            return m.Success ? m.Groups["c"].Value : ""; 
        }

        static HashSet<string> ExtractSet(string src, string name) {
            var set = new HashSet<string>();
            Match m = Regex.Match(src, name + @"\s*=\s*new\s*Set\(\[\s*(.*?)\s*\]\)", RegexOptions.Singleline);
            if (m.Success) foreach (Match im in Regex.Matches(m.Groups[1].Value, @"""(.*?)""|'(.*?)'")) set.Add(im.Groups[1].Value + im.Groups[2].Value);
            return set;
        }
    }
}