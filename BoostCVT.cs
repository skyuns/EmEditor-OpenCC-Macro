// Project: OpenCC for EmEditor Macro v0.44 BoostCVT
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
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("BoostCVT Engine")]
[assembly: AssemblyDescription("High-performance Text Converter for EmEditor")]
[assembly: AssemblyCompany("skyuns")]
[assembly: AssemblyProduct("BoostCVT")]
[assembly: AssemblyCopyright("Copyright © 2026 skyuns (天匀). All rights reserved.")]
[assembly: AssemblyVersion("0.44.0.0")]
[assembly: AssemblyFileVersion("0.44.0.0")]

namespace MyTools {
    public class TextProcessor {

    static int[] FastReplaceBmpMap;
    static int[] FastReplaceP2Map;
    static bool FastReplaceMapReady = false;

    // NFC 正規化生成的對齊字串
    static readonly string RawBmpHex = "8C48,66F4,8ECA,8CC8,6ED1,4E32,53E5,9F9C,9F9C,5951,91D1,5587,5948,61F6,7669,7F85,863F,87BA,88F8,908F,6A02,6D1B,70D9,73DE,843D,916A,99F1,4E82,5375,6B04,721B,862D,9E1E,5D50,6FEB,85CD,8964,62C9,81D8,881F,5ECA,6717,6D6A,72FC,90CE,4F86,51B7,52DE,64C4,6AD3,7210,76E7,8001,8606,865C,8DEF,9732,9B6F,9DFA,788C,797F,7DA0,83C9,9304,9E7F,8AD6,58DF,5F04,7C60,807E,7262,78CA,8CC2,96F7,58D8,5C62,6A13,6DDA,6F0F,7D2F,7E37,964B,52D2,808B,51DC,51CC,7A1C,7DBE,83F1,9675,8B80,62CF,6A02,8AFE,4E39,5BE7,6012,7387,7570,5317,78FB,4FBF,5FA9,4E0D,6CCC,6578,7D22,53C3,585E,7701,8449,8AAA,6BBA,8FB0,6C88,62FE,82E5,63A0,7565,4EAE,5169,51C9,6881,7CE7,826F,8AD2,91CF,52F5,5442,5973,5EEC,65C5,6FFE,792A,95AD,9A6A,9E97,9ECE,529B,66C6,6B77,8F62,5E74,6190,6200,649A,6F23,7149,7489,79CA,7DF4,806F,8F26,84EE,9023,934A,5217,52A3,54BD,70C8,88C2,8AAA,5EC9,5FF5,637B,6BAE,7C3E,7375,4EE4,56F9,5BE7,5DBA,601C,73B2,7469,7F9A,8046,9234,96F6,9748,9818,4F8B,79AE,91B4,96B8,60E1,4E86,50DA,5BEE,5C3F,6599,6A02,71CE,7642,84FC,907C,9F8D,6688,962E,5289,677B,67F3,6D41,6E9C,7409,7559,786B,7D10,985E,516D,622E,9678,502B,5D19,6DEA,8F2A,5F8B,6144,6817,7387,9686,5229,540F,5C65,6613,674E,68A8,6CE5,7406,75E2,7F79,88CF,88E1,91CC,96E2,533F,6EBA,541D,71D0,7498,85FA,96A3,9C57,9E9F,6797,6DCB,81E8,7ACB,7B20,7C92,72C0,7099,8B58,4EC0,8336,523A,5207,5EA6,62D3,7CD6,5B85,6D1E,66B4,8F3B,884C,964D,898B,5ED3,5140,55C0,0000,0000,585A,0000,6674,0000,0000,51DE,732A,76CA,793C,795E,7965,798F,9756,7CBE,7FBD,0000,8612,0000,8AF8,0000,0000,9038,90FD,0000,0000,0000,98EF,98FC,9928,9DB4,90DE,96B7,4FAE,50E7,514D,52C9,52E4,5351,559D,5606,5668,5840,58A8,5C64,5C6E,6094,6168,618E,61F2,654F,65E2,6691,6885,6D77,6E1A,6F22,716E,722B,7422,7891,793E,7949,7948,7950,7956,795D,798D,798E,7A40,7A81,7BC0,7DF4,7E09,7E41,7F72,8005,81ED,8279,8279,8457,8910,8996,8B01,8B39,8CD3,8D08,8FB6,9038,96E3,97FF,983B,6075,242EE,8218,0000,0000,4E26,51B5,5168,4F80,5145,5180,52C7,52FA,559D,5555,5599,55E2,585A,58B3,5944,5954,5A62,5B28,5ED2,5ED9,5F69,5FAD,60D8,614E,6108,618E,6160,61F2,6234,63C4,641C,6452,6556,6674,6717,671B,6756,6B79,6BBA,6D41,6EDB,6ECB,6F22,701E,716E,77A7,7235,72AF,732A,7471,7506,753B,761D,761F,76CA,76DB,76F4,774A,7740,78CC,7AB1,7BC0,7C7B,7D5B,7DF4,7F3E,8005,8352,83EF,8779,8941,8986,8996,8ABF,8AF8,8ACB,8B01,8AFE,8AED,8B39,8B8A,8D08,8F38,9072,9199,9276,967C,96E3,9756,97DB,97FF,980B,983B,9B12,9F9C,2284A,22844,233D5,3B9D,4018,4039,25249,25CD0,27ED3,9F43,9F8E";
    static readonly string RawP2Hex = "4E3D,4E38,4E41,20122,4F60,4FAE,4FBB,5002,507A,5099,50E7,50CF,349E,2063A,514D,5154,5164,5177,2051C,34B9,5167,518D,2054B,5197,51A4,4ECC,51AC,51B5,291DF,51F5,5203,34DF,523B,5246,5272,5277,3515,52C7,52C9,52E4,52FA,5305,5306,5317,5349,5351,535A,5373,537D,537F,537F,537F,20A2C,7070,53CA,53DF,20B63,53EB,53F1,5406,549E,5438,5448,5468,54A2,54F6,5510,5553,5563,5584,5584,5599,55AB,55B3,55C2,5716,5606,5717,5651,5674,5207,58EE,57CE,57F4,580D,578B,5832,5831,58AC,214E4,58F2,58F7,5906,591A,5922,5962,216A8,216EA,59EC,5A1B,5A27,59D8,5A66,36EE,36FC,5B08,5B3E,5B3E,219C8,5BC3,5BD8,5BE7,5BF3,21B18,5BFF,5C06,5F53,5C22,3781,5C60,5C6E,5CC0,5C8D,21DE4,5D43,21DE6,5D6E,5D6B,5D7C,5DE1,5DE2,382F,5DFD,5E28,5E3D,5E69,3862,22183,387C,5EB0,5EB3,5EB6,5ECA,2A392,5EFE,22331,22331,8201,5F22,5F22,38C7,232B8,261DA,5F62,5F6B,38E3,5F9A,5FCD,5FD7,5FF9,6081,393A,391C,6094,226D4,60C7,6148,614C,614E,614C,617A,618E,61B2,61A4,61AF,61DE,61F2,61F6,6210,621B,625D,62B1,62D4,6350,22B0C,633D,62FC,6368,6383,63E4,22BF1,6422,63C5,63A9,3A2E,6469,647E,649D,6477,3A6C,654F,656C,2300A,65E3,66F8,6649,3B19,6691,3B08,3AE4,5192,5195,6700,669C,80AD,43D9,6717,671B,6721,675E,6753,233C3,3B49,67FA,6785,6852,6885,2346D,688E,681F,6914,3B9D,6942,69A3,69EA,6AA8,236A3,6ADB,3C18,6B21,238A7,6B54,3C4E,6B72,6B9F,6BBA,6BBB,23A8D,21D0B,23AFA,6C4E,23CBC,6CBF,6CCD,6C67,6D16,6D3E,6D77,6D41,6D69,6D78,6D85,23D1E,6D34,6E2F,6E6E,3D33,6ECB,6EC7,23ED1,6DF9,6F6E,23F5E,23F8E,6FC6,7039,701E,701B,3D96,704A,707D,7077,70AD,20525,7145,24263,719C,243AB,7228,7235,7250,24608,7280,7295,24735,24814,737A,738B,3EAC,73A5,3EB8,3EB8,7447,745C,7471,7485,74CA,3F1B,7524,24C36,753E,24C92,7570,2219F,7610,24FA1,24FB8,25044,3FFC,4008,76F4,250F3,250F2,25119,25133,771E,771F,771F,774A,4039,778B,4046,4096,2541D,784E,788C,78CC,40E3,25626,7956,2569A,256C5,798F,79EB,412F,7A40,7A4A,7A4F,2597C,25AA7,25AA7,7AEE,4202,25BAB,7BC6,7BC9,4227,25C80,7CD2,42A0,7CE8,7CE3,7D00,25F86,7D63,4301,7DC7,7E02,7E45,4334,26228,26247,4359,262D9,7F7A,2633E,7F95,7FFA,8005,264DA,26523,8060,265A8,8070,2335F,43D5,80B2,8103,440B,813E,5AB5,267A7,267B5,23393,2339C,8201,8204,8F9E,446B,8291,828B,829D,52B3,82B1,82B3,82BD,82E6,26B3C,82E5,831D,8363,83AD,8323,83BD,83E7,8457,8353,83CA,83CC,83DC,26C36,26D6B,26CD5,452B,84F1,84F3,8516,273CA,8564,26F2C,455D,4561,26FB1,270D2,456B,8650,865C,8667,8669,86A9,8688,870E,86E2,8779,8728,876B,8786,45D7,87E1,8801,45F9,8860,8863,27667,88D7,88DE,4635,88FA,34BB,278AE,27966,46BE,46C7,8AA0,8AED,8B8A,8C55,27CA8,8CAB,8CC1,8D1B,8D77,27F2F,20804,8DCB,8DBC,8DF0,208DE,8ED4,8F38,285D2,285ED,9094,90F1,9111,2872E,911B,9238,92D7,92D8,927C,93F9,9415,28BFA,958B,4995,95B7,28D77,49E6,96C3,5DB2,9723,29145,2921A,4A6E,4A76,97E0,2940A,4AB2,29496,980B,980B,9829,295B6,98E2,4B33,9929,99A7,99C2,99FE,4BCE,29B30,9B12,9C40,9CFD,4CCE,4CED,9D67,2A0CE,4CF8,2A105,2A20E,2A291,9EBB,4D56,9EF9,9EFE,9F05,9F0F,9F16,9F3B,2A600";

    static void InitializeReplaceMap() {
        if (FastReplaceMapReady) return;

        // 解析 BMP 平面資料 (從 0xF900 開始)
        string[] bmpItems = RawBmpHex.Split(',');
        FastReplaceBmpMap = new int[bmpItems.Length];
        for (int i = 0; i < bmpItems.Length; i++) {
            string item = bmpItems[i];
            if (item == "0000" || string.IsNullOrEmpty(item)) continue;
            int targetCodePoint = System.Convert.ToInt32(item, 16);

            FastReplaceBmpMap[i] = targetCodePoint;
        }

        // 解析 Plane 2 平面資料 (從 0x2F800 開始)
        string[] p2Items = RawP2Hex.Split(',');
        FastReplaceP2Map = new int[p2Items.Length];
        for (int i = 0; i < p2Items.Length; i++) {
            string item = p2Items[i];
            if (item == "0000" || string.IsNullOrEmpty(item)) continue;
            int targetCodePoint = System.Convert.ToInt32(item, 16);

            FastReplaceP2Map[i] = targetCodePoint;
        }

        FastReplaceMapReady = true;
    }

    static int GetFastReplacement(int codePoint) {
        int index;
        if (codePoint >= 0xF900 && codePoint < 0xF900 + FastReplaceBmpMap.Length) {
            index = codePoint - 0xF900;
            return FastReplaceBmpMap[index];
        }
        if (codePoint >= 0x2F800 && codePoint < 0x2F800 + FastReplaceP2Map.Length) {
            index = codePoint - 0x2F800;
            return FastReplaceP2Map[index];
        }
        return 0;
    }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll")]
        static extern bool CloseClipboard();
        [DllImport("user32.dll")]
        static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll")]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll")]
        static extern bool EmptyClipboard();
        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")]
        static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        static extern void CopyMemory(IntPtr destination, IntPtr source, UIntPtr length);

        const uint CF_UNICODETEXT = 13;
        const uint GMEM_MOVEABLE = 0x0002;

        static string NativeGetClipboardText() {
            if (!OpenClipboard(IntPtr.Zero)) return string.Empty;
            IntPtr handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero) { CloseClipboard(); return string.Empty; }
            IntPtr pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) { CloseClipboard(); return string.Empty; }
            string text = Marshal.PtrToStringUni(pointer);
            GlobalUnlock(handle);
            CloseClipboard();
            return text;
        }

        static void NativeSetClipboardText(string text) {
            if (text == null) return;
            if (!OpenClipboard(IntPtr.Zero)) return;
            EmptyClipboard();
            UIntPtr bytes = new UIntPtr((uint)(text.Length + 1) * 2);
            IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, bytes);
            if (hMem != IntPtr.Zero) {
                IntPtr pointer = GlobalLock(hMem);
                if (pointer != IntPtr.Zero) {
                    Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                    Marshal.WriteInt16(pointer, text.Length * 2, 0);
                    GlobalUnlock(hMem);
                    SetClipboardData(CF_UNICODETEXT, hMem);
                }
            }
            CloseClipboard();
        }

        static void NativeSetClipboardChunks(string[] chunks) {
            if (chunks == null) return;

            long totalLength = 0;
            for (int i = 0; i < chunks.Length; i++) {
                if (chunks[i] != null) totalLength += chunks[i].Length;
            }
            if (totalLength > 2147483646L) return;

            if (!OpenClipboard(IntPtr.Zero)) return;
            EmptyClipboard();

            UIntPtr bytes = new UIntPtr((ulong)(totalLength + 1) * 2UL);
            IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, bytes);
            if (hMem != IntPtr.Zero) {
                IntPtr destinationBase = GlobalLock(hMem);
                if (destinationBase != IntPtr.Zero) {
                    long targetCharOffset = 0;

                    for (int i = 0; i < chunks.Length; i++) {
                        string chunk = chunks[i];
                        if (string.IsNullOrEmpty(chunk)) continue;

                        GCHandle pinnedChunk = default(GCHandle);
                        try {
                            // 固定字串後直接複製 UTF-16 記憶體，避免經過 8 KB char[] 中繼緩衝區。
                            pinnedChunk = GCHandle.Alloc(chunk, GCHandleType.Pinned);
                            IntPtr sourcePointer = pinnedChunk.AddrOfPinnedObject();
                            IntPtr destinationPointer = new IntPtr(
                                destinationBase.ToInt64() + targetCharOffset * 2L);

                            CopyMemory(
                                destinationPointer,
                                sourcePointer,
                                new UIntPtr((ulong)chunk.Length * 2UL));

                            targetCharOffset += chunk.Length;
                        } finally {
                            if (pinnedChunk.IsAllocated) pinnedChunk.Free();
                        }
                    }

                    Marshal.WriteInt16(
                        new IntPtr(destinationBase.ToInt64() + targetCharOffset * 2L),
                        0);

                    GlobalUnlock(hMem);
                    SetClipboardData(CF_UNICODETEXT, hMem);
                }
            }

            CloseClipboard();
        }

        class TrieNode { 
            struct ChildEntry {
                public char Key;
                public TrieNode Node;
            }

            ChildEntry[] SmallChildren;
            int SmallChildCount;
            Dictionary<char, TrieNode> LargeChildren;

            public string Value;
            public ulong FastMask;
            public ulong FastMask2; // 第二指紋：字元第 6~11 位，降低第一層低 6 位碰撞
            public string OriginalKey;
            public double LogFreq = -18.0; // Jieba 分詞對數頻率
            public double JiebaBaseScore; // DAG 候選詞固定加扣分預先計算結果

            public bool IsVisionAnchor; 
            public bool IsVisionVocab; 
            public bool IsContextAnchor;

            public bool HasChildren {
                get { return LargeChildren != null || SmallChildCount != 0; }
            }

            public bool TryGetChild(char key, out TrieNode node) {
                if (LargeChildren != null) {
                    return LargeChildren.TryGetValue(key, out node);
                }

                if (SmallChildCount > 0 && SmallChildren[0].Key == key) {
                    node = SmallChildren[0].Node;
                    return true;
                }
                if (SmallChildCount > 1 && SmallChildren[1].Key == key) {
                    node = SmallChildren[1].Node;
                    return true;
                }
                if (SmallChildCount > 2 && SmallChildren[2].Key == key) {
                    node = SmallChildren[2].Node;
                    return true;
                }
                if (SmallChildCount > 3 && SmallChildren[3].Key == key) {
                    node = SmallChildren[3].Node;
                    return true;
                }

                node = null;
                return false;
            }

            public void PrepareJiebaBaseScore(
                int wordLen,
                double dictBonus2,
                double dictBonus3,
                double dictBonus4,
                double freqBonusGodly,
                double freqBonusLegendary,
                double freqBonusEpic,
                double freqBonusElite,
                double freqBonusHigh,
                double freqBonusMid,
                double freqBonusLow,
                double freqBonusFew,
                double penaltyVision2,
                double penaltyVision3,
                double penaltyCtx,
                double bonusVisionVocab) {

                double wordFreq = LogFreq;

                // A：詞典加分機制預先計算
                double dictBonus = 0;
                if (wordLen > 1 && Value != null) {
                    if (wordLen >= 4) dictBonus = dictBonus4;
                    else if (wordLen == 3) dictBonus = dictBonus3;
                    else dictBonus = dictBonus2;
                }

                // A-2：結巴詞頻分層加分 (8階梯) + 長度給分預先計算
                double freqBonus = 0;
                if (wordLen > 1 && LogFreq != -18.0) {
                    if (wordFreq > -9.2) freqBonus = freqBonusGodly;
                    else if (wordFreq > -9.9) freqBonus = freqBonusLegendary;
                    else if (wordFreq > -10.3) freqBonus = freqBonusEpic;
                    else if (wordFreq > -11.0) freqBonus = freqBonusElite;
                    else if (wordFreq > -13.3) freqBonus = freqBonusHigh;
                    else if (wordFreq > -15.5) freqBonus = freqBonusMid;
                    else if (wordFreq > -16.5) freqBonus = freqBonusLow;
                    else freqBonus = freqBonusFew;

                    if (wordLen >= 4) freqBonus += 9.0;
                    else if (wordLen == 3) freqBonus += 3.0;
                }

                // C：Set 精確加扣分預先計算
                double extraWeight = 0;
                if (wordLen > 1) {
                    if (IsVisionAnchor) {
                        extraWeight += (wordLen == 2) ? penaltyVision2 : penaltyVision3;
                    }
                    if (IsContextAnchor) {
                        extraWeight += penaltyCtx;
                    }
                    if (IsVisionVocab) {
                        extraWeight += bonusVisionVocab;
                    }
                }

                JiebaBaseScore = wordFreq + dictBonus + freqBonus + extraWeight;

                if (LargeChildren != null) {
                    foreach (KeyValuePair<char, TrieNode> child in LargeChildren) {
                        child.Value.PrepareJiebaBaseScore(
                            wordLen + 1,
                            dictBonus2, dictBonus3, dictBonus4,
                            freqBonusGodly, freqBonusLegendary, freqBonusEpic, freqBonusElite,
                            freqBonusHigh, freqBonusMid, freqBonusLow, freqBonusFew,
                            penaltyVision2, penaltyVision3, penaltyCtx, bonusVisionVocab);
                    }
                } else {
                    for (int i = 0; i < SmallChildCount; i++) {
                        SmallChildren[i].Node.PrepareJiebaBaseScore(
                            wordLen + 1,
                            dictBonus2, dictBonus3, dictBonus4,
                            freqBonusGodly, freqBonusLegendary, freqBonusEpic, freqBonusElite,
                            freqBonusHigh, freqBonusMid, freqBonusLow, freqBonusFew,
                            penaltyVision2, penaltyVision3, penaltyCtx, bonusVisionVocab);
                    }
                }
            }

            public TrieNode GetOrAddChild(char key) {
                TrieNode node;
                if (TryGetChild(key, out node)) return node;

                node = new TrieNode();
                if (LargeChildren != null) {
                    LargeChildren.Add(key, node);
                    return node;
                }

                if (SmallChildCount < 4) {
                    if (SmallChildren == null) {
                        SmallChildren = new ChildEntry[2];
                    } else if (SmallChildCount == SmallChildren.Length) {
                        ChildEntry[] expanded = new ChildEntry[4];
                        Array.Copy(SmallChildren, expanded, SmallChildCount);
                        SmallChildren = expanded;
                    }

                    SmallChildren[SmallChildCount].Key = key;
                    SmallChildren[SmallChildCount].Node = node;
                    SmallChildCount++;
                    return node;
                }

                LargeChildren = new Dictionary<char, TrieNode>(8);
                for (int i = 0; i < SmallChildCount; i++) {
                    LargeChildren.Add(SmallChildren[i].Key, SmallChildren[i].Node);
                }
                SmallChildren = null;
                SmallChildCount = 0;
                LargeChildren.Add(key, node);
                return node;
            }
        }

        // HMM 模型結構定義
        class HmmModel {

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

        // ContextLogic 微型規則結構
        class ContextRule {
            public List<ContextCondition> Conditions = new List<ContextCondition>();
            public ContextCondition[] ConditionArray;
            public string Target;
        }

        class ContextCondition {
            public int Offset;
            public bool IsExclude;
            public string CharSet;
            public bool IsSn2;
        }

        static Dictionary<char, List<ContextRule>> FastContextRules = new Dictionary<char, List<ContextRule>>();
        static ContextRule[][] FastContextRuleArray = new ContextRule[65536][];

        // PhraseLogic 邏輯
        class PhraseRule {
            public string Key;
            public string Target;

            public List<string> LeftIncludes = new List<string>();
            public List<string> LeftExcludes = new List<string>();
            public List<string> RightIncludes = new List<string>();
            public List<string> RightExcludes = new List<string>();

            // 轉換熱路徑用：LoadPhraseLogic 階段預拆 @精確比對與 contains 比對，避免每次 Substring/foreach/List 判斷。
            public string[] LeftAtIncludes;
            public string[] LeftContainsIncludes;
            public string[] LeftAtExcludes;
            public string[] LeftContainsExcludes;
            public string[] RightAtIncludes;
            public string[] RightContainsIncludes;
            public string[] RightAtExcludes;
            public string[] RightContainsExcludes;
            public int IncludeRuleCount;

            public int WindowSize = 6;
            public int PunctuationMode = 1; // 預設 1 為穿越，0 為不穿越防火牆
            public int LengthThreshold = 4; // 門檻參數
        }

        // BoostCVT 專用的精簡 JSEE 結構擷取器。
        sealed class JseeStructureExtractor {
            readonly string source;
            readonly Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.Ordinal);

            public JseeStructureExtractor(string text) {
                source = text ?? "";
                IndexConstants();
            }

            static bool IsIdStart(char c) {
                return char.IsLetter(c) || c == '_' || c == '$';
            }

            static bool IsIdChar(char c) {
                return char.IsLetterOrDigit(c) || c == '_' || c == '$';
            }

            int SkipQuoted(int p) {
                char quote = source[p++];
                bool escaped = false;
                while (p < source.Length) {
                    char c = source[p++];
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == quote) return p;
                }
                return source.Length;
            }

            int SkipTrivia(int p) {
                while (p < source.Length) {
                    if (char.IsWhiteSpace(source[p])) { p++; continue; }
                    if (p + 1 < source.Length && source[p] == '/') {
                        if (source[p + 1] == '/') {
                            p += 2;
                            while (p < source.Length && source[p] != '\r' && source[p] != '\n') p++;
                            continue;
                        }
                        if (source[p + 1] == '*') {
                            p += 2;
                            while (p + 1 < source.Length && !(source[p] == '*' && source[p + 1] == '/')) p++;
                            if (p + 1 < source.Length) p += 2;
                            continue;
                        }
                    }
                    break;
                }
                return p;
            }

            void IndexConstants() {
                int p = 0;
                while (p < source.Length) {
                    char c = source[p];
                    if (c == '"' || c == '\'' || c == '`') { p = SkipQuoted(p); continue; }
                    if (p + 1 < source.Length && c == '/' && source[p + 1] == '/') {
                        p += 2;
                        while (p < source.Length && source[p] != '\r' && source[p] != '\n') p++;
                        continue;
                    }
                    if (p + 1 < source.Length && c == '/' && source[p + 1] == '*') {
                        p += 2;
                        while (p + 1 < source.Length && !(source[p] == '*' && source[p + 1] == '/')) p++;
                        if (p + 1 < source.Length) p += 2;
                        continue;
                    }
                    if (!IsIdStart(c)) { p++; continue; }

                    int token = p++;
                    while (p < source.Length && IsIdChar(source[p])) p++;
                    if (p - token != 5 || string.CompareOrdinal(source, token, "const", 0, 5) != 0) continue;

                    p = SkipTrivia(p);
                    if (p >= source.Length || !IsIdStart(source[p])) continue;
                    int nameStart = p++;
                    while (p < source.Length && IsIdChar(source[p])) p++;
                    string name = source.Substring(nameStart, p - nameStart);
                    p = SkipTrivia(p);
                    if (p >= source.Length || source[p] != '=') continue;
                    p = SkipTrivia(p + 1);
                    if (p < source.Length && !values.ContainsKey(name)) values.Add(name, p);
                }
            }

            int Match(int open, char left, char right) {
                if (open < 0 || open >= source.Length || source[open] != left) return -1;
                int depth = 0;
                for (int p = open; p < source.Length; p++) {
                    char c = source[p];
                    if (c == '"' || c == '\'' || c == '`') { p = SkipQuoted(p) - 1; continue; }
                    if (p + 1 < source.Length && c == '/' && source[p + 1] == '/') {
                        p += 2;
                        while (p < source.Length && source[p] != '\r' && source[p] != '\n') p++;
                        continue;
                    }
                    if (p + 1 < source.Length && c == '/' && source[p + 1] == '*') {
                        p += 2;
                        while (p + 1 < source.Length && !(source[p] == '*' && source[p + 1] == '/')) p++;
                        if (p + 1 < source.Length) p++;
                        continue;
                    }
                    if (c == left) depth++;
                    else if (c == right && --depth == 0) return p;
                }
                return -1;
            }

            bool TryBody(string name, char left, char right, out string body) {
                body = null;
                int p, end;
                if (!values.TryGetValue(name, out p) || p >= source.Length || source[p] != left) return false;
                end = Match(p, left, right);
                if (end < 0) return false;
                body = source.Substring(p + 1, end - p - 1);
                return true;
            }

            public bool TryGetConstString(string name, out string value) {
                value = null;
                int p;
                if (!values.TryGetValue(name, out p) || p >= source.Length || (source[p] != '"' && source[p] != '\'')) return false;
                int end = SkipQuoted(p);
                if (end <= p + 1 || source[end - 1] != source[p]) return false;
                value = source.Substring(p + 1, end - p - 2);
                return true;
            }

            public bool TryGetConstTemplate(string name, out string value) {
                value = null;
                int p;
                if (!values.TryGetValue(name, out p) || p >= source.Length || source[p] != '`') return false;
                int end = SkipQuoted(p);
                if (end <= p + 1 || source[end - 1] != '`') return false;
                value = source.Substring(p + 1, end - p - 2);
                return true;
            }

            public bool TryGetConstObjectBody(string name, out string body) {
                return TryBody(name, '{', '}', out body);
            }

            public bool TryGetConstSetBody(string name, out string body) {
                body = null;
                int p;
                if (!values.TryGetValue(name, out p) || p + 3 > source.Length ||
                    string.CompareOrdinal(source, p, "new", 0, 3) != 0) return false;
                p = SkipTrivia(p + 3);
                if (p + 3 > source.Length || string.CompareOrdinal(source, p, "Set", 0, 3) != 0) return false;
                p = SkipTrivia(p + 3);
                if (p >= source.Length || source[p] != '(') return false;
                p = SkipTrivia(p + 1);
                if (p >= source.Length || source[p] != '[') return false;
                int end = Match(p, '[', ']');
                if (end < 0) return false;
                body = source.Substring(p + 1, end - p - 1);
                return true;
            }
        }

        static Dictionary<char, List<PhraseRule>> FastPhraseRules = new Dictionary<char, List<PhraseRule>>();
        static PhraseRule[][] FastPhraseRuleArray = new PhraseRule[65536][];
        static bool[] HasPhraseLogicStart = new bool[65536];
        static int[] MaxPhraseThreshold = new int[65536];

        static bool[] IsBarrierSymbol = new bool[65536];

        static readonly string[] EmptyStringArray = new string[0];

        static void SplitPhraseTags(List<string> rawTags, out string[] atTags, out string[] containsTags) {
            if (rawTags == null || rawTags.Count == 0) {
                atTags = EmptyStringArray;
                containsTags = EmptyStringArray;
                return;
            }
            List<string> atList = null;
            List<string> containsList = null;
            for (int i = 0; i < rawTags.Count; i++) {
                string tag = rawTags[i];
                if (string.IsNullOrEmpty(tag)) continue;
                if (tag[0] == '@') {
                    if (atList == null) atList = new List<string>();
                    atList.Add(tag.Substring(1));
                } else {
                    if (containsList == null) containsList = new List<string>();
                    containsList.Add(tag);
                }
            }
            atTags = atList != null ? atList.ToArray() : EmptyStringArray;
            containsTags = containsList != null ? containsList.ToArray() : EmptyStringArray;
        }

        static void PreparePhraseRule(PhraseRule rule) {
            SplitPhraseTags(rule.LeftIncludes, out rule.LeftAtIncludes, out rule.LeftContainsIncludes);
            SplitPhraseTags(rule.LeftExcludes, out rule.LeftAtExcludes, out rule.LeftContainsExcludes);
            SplitPhraseTags(rule.RightIncludes, out rule.RightAtIncludes, out rule.RightContainsIncludes);
            SplitPhraseTags(rule.RightExcludes, out rule.RightAtExcludes, out rule.RightContainsExcludes);
            rule.IncludeRuleCount = rule.LeftAtIncludes.Length + rule.LeftContainsIncludes.Length + rule.RightAtIncludes.Length + rule.RightContainsIncludes.Length;
        }

        static bool IsExactMatchAt(string input, int start, string target) {
            if (target == null) return false;
            if (start < 0 || start + target.Length > input.Length) return false;
            for (int i = 0; i < target.Length; i++) {
                if (input[start + i] != target[i]) return false;
            }
            return true;
        }

        static bool HasLeftAtMatch(string input, int absoluteIdx, string[] tags) {
            if (tags == null) return false;
            for (int i = 0; i < tags.Length; i++) {
                string tag = tags[i];
                if (IsExactMatchAt(input, absoluteIdx - tag.Length, tag)) return true;
            }
            return false;
        }

        static bool HasRightAtMatch(string input, int rightStart, string[] tags) {
            if (tags == null) return false;
            for (int i = 0; i < tags.Length; i++) {
                if (IsExactMatchAt(input, rightStart, tags[i])) return true;
            }
            return false;
        }

        static bool HasContainsMatch(string input, int viewStart, int viewLen, string[] tags) {
            if (tags == null || viewLen <= 0) return false;
            for (int i = 0; i < tags.Length; i++) {
                if (IntrospectiveContains(input, viewStart, viewLen, tags[i])) return true;
            }
            return false;
        }

        static void LoadPhraseLogic(string source, JseeStructureExtractor extractor) {
            FastPhraseRules.Clear();
            Array.Clear(FastPhraseRuleArray, 0, FastPhraseRuleArray.Length);
            Array.Clear(HasPhraseLogicStart, 0, HasPhraseLogicStart.Length);
            Array.Clear(MaxPhraseThreshold, 0, MaxPhraseThreshold.Length);
            Array.Clear(IsBarrierSymbol, 0, IsBarrierSymbol.Length);

            // 標點符號防火牆
            string barriers = "。.，,！!？?；;…\n\r";

            foreach (char c in barriers) IsBarrierSymbol[c] = true;

            string phraseLogicBody;
            if (!extractor.TryGetConstObjectBody("PhraseLogic", out phraseLogicBody)) {
                Match legacyMatch = Regex.Match(source, @"const\s+PhraseLogic\s*=\s*\{(.*?)\};", RegexOptions.Singleline);
                if (!legacyMatch.Success) return;
                phraseLogicBody = legacyMatch.Groups[1].Value;
            }
            var matches = Regex.Matches(phraseLogicBody, @"""(?<k>[^""]+)"":\s*""(?<v>[^""]+)""");

            foreach (Match entry in matches) {
                string k = entry.Groups["k"].Value;
                string v = entry.Groups["v"].Value;
                v = v.Replace('\t', ' ');

                PhraseRule rule = new PhraseRule();
                rule.Key = k;

                // 解析後綴參數
                int wSize = 6;
                int pMode = 1;
                int lThres = 4;

                Match paramMatch = Regex.Match(v, @"\{(?<w>[1-9])\s*,\s*(?<p>[01])(?:\s*,\s*(?<t>[4-9]))?\}");
                if (paramMatch.Success) {
                    int.TryParse(paramMatch.Groups["w"].Value, out wSize);
                    int.TryParse(paramMatch.Groups["p"].Value, out pMode);
                    if (paramMatch.Groups["t"].Success) {
                        int.TryParse(paramMatch.Groups["t"].Value, out lThres);
                    }
                    v = v.Substring(0, paramMatch.Index).Trim();
                }
                rule.WindowSize = wSize;
                rule.PunctuationMode = pMode;
                rule.LengthThreshold = lThres;

                var parts = v.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                var targets = parts[0].Split(' ');

                string rawTarget = targets.Length > 1 ? targets[1] : targets[0];
                rule.Target = rawTarget;

                // 解析左權重
                if (parts.Length > 1) {
                    foreach (var tag in parts[1].Split('|')) {
                        if (string.IsNullOrEmpty(tag)) continue;
                        if (tag[0] == '!') {
                            rule.LeftExcludes.Add(tag.Substring(1));
                        } else {
                            rule.LeftIncludes.Add(tag);
                        }
                    }
                }

                // 解析右權重
                if (parts.Length > 2) {
                    foreach (var tag in parts[2].Split('|')) {
                        if (string.IsNullOrEmpty(tag)) continue;
                        if (tag[0] == '!') {
                            rule.RightExcludes.Add(tag.Substring(1));
                        } else {
                            rule.RightIncludes.Add(tag);
                        }
                    }
                }

                if (k.Length > 0) {
                    char firstChar = k[0];
                    if (!HasPhraseLogicStart[firstChar]) {
                        HasPhraseLogicStart[firstChar] = true;
                        FastPhraseRules[firstChar] = new List<PhraseRule>();
                        MaxPhraseThreshold[firstChar] = 4;
                    }

                    if (rule.LengthThreshold > MaxPhraseThreshold[firstChar]) {
                        MaxPhraseThreshold[firstChar] = rule.LengthThreshold;
                    }
                    PreparePhraseRule(rule);
                    FastPhraseRules[firstChar].Add(rule);
                }
            }

            foreach (KeyValuePair<char, List<PhraseRule>> kvp in FastPhraseRules) {
                FastPhraseRuleArray[kvp.Key] = kvp.Value.ToArray();
            }
        }

        static bool TryApplyPhraseLogic(int i, int start, int len, string input, ref int localReplaceCount, out string matchedTarget, out int matchLen) {
            matchedTarget = null;
            matchLen = 0;

            char firstChar = input[start + i];
            if (!HasPhraseLogicStart[firstChar]) return false;

            PhraseRule[] rules = FastPhraseRuleArray[firstChar];
            if (rules == null || rules.Length == 0) return false;

            int absoluteIdx = start + i;
            PhraseRule targetRule = null;
            int maxKeyLen = 0;

            for (int rIdx = 0; rIdx < rules.Length; rIdx++) {
                PhraseRule rule = rules[rIdx];
                string pKey = rule.Key;
                int pKeyLen = pKey.Length;

                if (pKeyLen > maxKeyLen && i + pKeyLen <= len) {
                    bool keyMatch = true;
                    for (int k = 0; k < pKeyLen; k++) {
                        if (input[absoluteIdx + k] != pKey[k]) { keyMatch = false; break; }
                    }

                    if (keyMatch) {
                        maxKeyLen = pKeyLen;
                        targetRule = rule;
                    }
                }
            }

            if (targetRule == null) return false;

            PhraseRule finalRule = targetRule;
            string finalKey = finalRule.Key;
            int finalKeyLen = finalKey.Length;

            int currentWinSize = finalRule.WindowSize;
            bool pBarrierActive = finalRule.PunctuationMode == 0;

            // 向左計算視窗
            int leftStart = absoluteIdx;
            int scanLeftCount = 0;
            while (leftStart > 0 && scanLeftCount < currentWinSize) {
                char leftChar = input[leftStart - 1];
                if (pBarrierActive && IsBarrierSymbol[leftChar]) break;
                leftStart--;
                scanLeftCount++;
            }
            int leftLen = absoluteIdx - leftStart;

            // 向右計算視窗
            int rightStart = absoluteIdx + finalKeyLen;
            int rightEndLimit = Math.Min(start + len, rightStart + currentWinSize);
            int rightScanEnd = rightStart;
            while (rightScanEnd < rightEndLimit) {
                char rightChar = input[rightScanEnd];
                if (pBarrierActive && IsBarrierSymbol[rightChar]) break;
                rightScanEnd++;
            }
            int rightLen = rightScanEnd - rightStart;

            if (HasLeftAtMatch(input, absoluteIdx, finalRule.LeftAtExcludes) ||
                HasContainsMatch(input, leftStart, leftLen, finalRule.LeftContainsExcludes) ||
                HasRightAtMatch(input, rightStart, finalRule.RightAtExcludes) ||
                HasContainsMatch(input, rightStart, rightLen, finalRule.RightContainsExcludes)) {
                return false;
            }

            bool isTriggered =
                HasLeftAtMatch(input, absoluteIdx, finalRule.LeftAtIncludes) ||
                HasContainsMatch(input, leftStart, leftLen, finalRule.LeftContainsIncludes) ||
                HasRightAtMatch(input, rightStart, finalRule.RightAtIncludes) ||
                HasContainsMatch(input, rightStart, rightLen, finalRule.RightContainsIncludes);

            if (finalRule.IncludeRuleCount == 0 || isTriggered) {
                localReplaceCount++;
                matchedTarget = finalRule.Target;
                matchLen = finalKeyLen;
                return true;
            }

            return false;
        }

        static bool IntrospectiveContains(string src, int viewStart, int viewLen, string target) {
            return src.IndexOf(target, viewStart, viewLen, StringComparison.Ordinal) >= 0;
        }

        static Func<int, int, string, string> LogicDelegate = null;
        static bool[] HasContextLogic = new bool[65536];

        sealed class CharWriter {
            char[] buffer;
            int length;

            public CharWriter(int capacity) {
                buffer = new char[capacity > 0 ? capacity : 16];
                length = 0;
            }

            public int Length {
                get { return length; }
                set {
                    if (value < 0) value = 0;
                    EnsureCapacity(value);
                    length = value;
                }
            }

            public int Capacity {
                get { return buffer.Length; }
                set { EnsureCapacity(value); }
            }

            void EnsureCapacity(int needed) {
                if (needed <= buffer.Length) return;
                int newSize = buffer.Length;
                if (newSize < 16) newSize = 16;
                while (newSize < needed) {
                    int grown = newSize * 2;
                    if (grown <= newSize) { newSize = needed; break; }
                    newSize = grown;
                }
                char[] next = new char[newSize];
                if (length > 0) Array.Copy(buffer, 0, next, 0, length);
                buffer = next;
            }

            public void Append(char c) {
                EnsureCapacity(length + 1);
                buffer[length++] = c;
            }

            public void Append(string value) {
                if (string.IsNullOrEmpty(value)) return;
                int valueLen = value.Length;
                EnsureCapacity(length + valueLen);
                value.CopyTo(0, buffer, length, valueLen);
                length += valueLen;
            }

            public void Append(string value, int startIndex, int count) {
                if (string.IsNullOrEmpty(value) || count <= 0) return;
                EnsureCapacity(length + count);
                value.CopyTo(startIndex, buffer, length, count);
                length += count;
            }

            public override string ToString() {
                return length == 0 ? string.Empty : new string(buffer, 0, length);
            }
        }

        // 執行緒專屬記憶體池 (Zero-Allocation 核心)
        class ThreadState {
            public double[] routeScore = new double[1024];
            public int[] routeNext = new int[1024];
            public TrieNode[] routeNode = new TrieNode[1024];
            public double[] vBuf = new double[4096];
            public int[] bpBuf = new int[4096];
            public int[] spBuf = new int[1024];
            public CharWriter sb = new CharWriter(1024);
            public CharWriter fbSb = new CharWriter(128);

            public void EnsureSize(int len) {
                if (routeScore.Length < len + 1) {
                    int newSize = len + 4096;
                    routeScore = new double[newSize];
                    routeNext = new int[newSize];
                    routeNode = new TrieNode[newSize];
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
            InitializeReplaceMap();
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
                    JseeStructureExtractor sourceJseeExtractor = new JseeStructureExtractor(sourceJsee);

                    Func<string, string, bool, string> LoadData = (blockName, fileName, force) => {
                        if (string.IsNullOrEmpty(fileName)) return "";
                        string txtPath = Path.Combine(targetDictDir, fileName);
                        if (!File.Exists(txtPath)) return "";

                        if ((hasNewerUpdate || force) && File.GetLastWriteTime(txtPath) > dictJseeDate || force) {
                            return File.ReadAllText(txtPath, Encoding.UTF8);
                        }
                        if (!hasNewerUpdate && !force) return "";
                        if (!string.IsNullOrEmpty(sourceJsee)) return ExtractBlock(sourceJsee, blockName, sourceJseeExtractor);
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

                    try { NativeSetClipboardText(output); } catch { return 0; }
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
                JseeStructureExtractor jseeExtractor = new JseeStructureExtractor(source);
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
                    return ExtractBlock(source, blockName, jseeExtractor);
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

                LoadPhraseLogic(source, jseeExtractor);

                if (string.IsNullOrEmpty(rawPhraseDataStr) && string.IsNullOrEmpty(rawCharDataStr) && (isS2T || isT2S)) {
                    MessageBox.Show("Dictionary data missing.", "BoostCVT", MessageBoxButtons.OK);
                    Environment.Exit(-1);
                }

                VisionAnchors.Clear(); VisionVocabs.Clear(); ContextLogicAnchors.Clear();
                LogicDelegate = null; Array.Clear(HasContextLogic, 0, HasContextLogic.Length);

                // 結巴有開，就載入 Vision 陣列作為加扣分依據
                if ((isVis || isJiebaActive) && !isShift) {
                    VisionAnchors = ExtractSet(source, "VisionAnchors", jseeExtractor);
                    VisionVocabs = ExtractSet(source, "VisionVocabs", jseeExtractor);
                }

                if (isCtx && !isShift) {
                    ContextLogicAnchors = ExtractSet(source, "ContextLogicAnchors", jseeExtractor);
                    CompileContextLogic(source, mode, jseeExtractor);
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
                    foreach (char c in str) {
                        char mappedChar;
                        sb.Append(variantMap.TryGetValue(c, out mappedChar) ? mappedChar : c);
                    }
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

                                string target;
                                if (!exceptionMap.TryGetValue(key, out target)) {
                                    if (!twPhrasesMap.TryGetValue(firstTarget, out target)) {
                                        target = applyTWVariants(firstTarget);
                                    }
                                }
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

                LoadPhraseLogic(source, jseeExtractor);
                foreach (var kvp in FastPhraseRules) {
                    foreach (var rule in kvp.Value) {
                        if (!string.IsNullOrEmpty(rule.Target)) {
                            rule.Target = applyTWVariants(rule.Target);
                        }
                    }
                }

                if (isS2T && !isAlt && !isShift && twPhrasesMap.Count > 0 && reverseCharMap.Count > 0) {
                    foreach (var kvp in twPhrasesMap) {
                        string twKey = kvp.Key;
                        string twVal = kvp.Value;
                        StringBuilder scKeySb = new StringBuilder(twKey.Length);

                        for (int i = 0; i < twKey.Length; i++) {
                            char tcChar = twKey[i];
                            // 原字形精確反查
                            string reverseChar;
                            if (reverseCharMap.TryGetValue(tcChar, out reverseChar)) {
                                scKeySb.Append(reverseChar);
                            } else {
                                // 嘗試降維再查
                                char mappedChar;
                                char normChar = variantMap.TryGetValue(tcChar, out mappedChar) ? mappedChar : tcChar;
                                scKeySb.Append(reverseCharMap.TryGetValue(normChar, out reverseChar) ? reverseChar : normChar.ToString());
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
                            string genericTarget;
                            if (finalDict.TryGetValue(genericChar, out genericTarget)) finalDict[twChar] = genericTarget;
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
                    Dictionary<int, double> jiebaLogFreqCache = new Dictionary<int, double>(4096);
                    int dataLength = rawJiebaDataStr.Length;
                    int lineStart = 0;

                    // 直接掃描原始字典字串，避免 58 萬行的 ReadLine、Substring 與詞頻字串配置。
                    while (lineStart < dataLength) {
                        int lineEnd = lineStart;
                        while (lineEnd < dataLength && rawJiebaDataStr[lineEnd] != '\n') lineEnd++;

                        int contentEnd = lineEnd;
                        if (contentEnd > lineStart && rawJiebaDataStr[contentEnd - 1] == '\r') contentEnd--;

                        if (contentEnd > lineStart && rawJiebaDataStr[lineStart] != '#') {
                            int space1 = lineStart;
                            while (space1 < contentEnd && rawJiebaDataStr[space1] != ' ') space1++;

                            if (space1 > lineStart && space1 < contentEnd) {
                                int freqStart = space1 + 1;
                                int freqEnd = freqStart;
                                while (freqEnd < contentEnd && rawJiebaDataStr[freqEnd] != ' ') freqEnd++;

                                double logFreq;
                                if (TryGetJiebaLogFreq(
                                    rawJiebaDataStr,
                                    freqStart,
                                    freqEnd - freqStart,
                                    logTotal,
                                    jiebaLogFreqCache,
                                    out logFreq)) {

                                    AddJiebaWord(
                                        rootNodes,
                                        rawJiebaDataStr,
                                        lineStart,
                                        space1 - lineStart,
                                        logFreq);
                                }
                            }
                        }

                        lineStart = lineEnd + 1;
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

                                    double userLogFreq;
                                    int integerFreq = (freq > 0 && freq <= int.MaxValue) ? (int)freq : 0;
                                    if (integerFreq > 0 && freq == integerFreq) {
                                        if (!jiebaLogFreqCache.TryGetValue(integerFreq, out userLogFreq)) {
                                            userLogFreq = Math.Log(freq) - logTotal;
                                            jiebaLogFreqCache[integerFreq] = userLogFreq;
                                        }
                                    } else {
                                        userLogFreq = Math.Log(freq) - logTotal;
                                    }
                                    AddWord(rootNodes, word, null, userLogFreq);
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

                string fullInput = NativeGetClipboardText();
                if (string.IsNullOrEmpty(fullInput)) return 0;
                int totalLen = fullInput.Length;

                // 微型斷句隔離 (Micro-Chunking) 
                int targetChunkSize =
                    mode.StartsWith("T2S", StringComparison.Ordinal) && !isJiebaActive
                        ? 262144
                        : 65536; 
                int fastSkipLimit = (mode == "S2TWP" || mode == "TW2SP") ? 0x21 : 0x80;
                List<int> cutsList = new List<int>();
                cutsList.Add(0);
                int expectedPos = 0;

                while (expectedPos + targetChunkSize < totalLen) {
                    int searchStart = expectedPos + targetChunkSize;
                    int searchLength = Math.Min(targetChunkSize * 3, totalLen - searchStart);
                    int safeCut = fullInput.IndexOf('\n', searchStart, searchLength);

                    if (safeCut != -1) {
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
                int[] chunkReplaceCounts = new int[totalChunks];
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

                // 動態加扣分常數
                double PENALTY_VISION_2 = -2.5; // VisionAnchors 2字詞扣分
                double PENALTY_VISION_3 = -0.5; // VisionAnchors 3字詞(含以上)扣分
                double PENALTY_CTX = -0.5; // ContextLogicAnchors 扣分
                double BONUS_VISION_VOCAB = 15.0; // VisionVocabs 額外加分

                // Jieba DAG 候選詞固定加扣分預先計算，避免在最內層重複判斷
                if (isJiebaActive) {
                    for (int rootIndex = 0; rootIndex < rootNodes.Length; rootIndex++) {
                        TrieNode rootNode = rootNodes[rootIndex];
                        if (rootNode != null) {
                            rootNode.PrepareJiebaBaseScore(
                                1,
                                DICT_BONUS_2, DICT_BONUS_3, DICT_BONUS_4,
                                FREQ_BONUS_GODLY, FREQ_BONUS_LEGENDARY, FREQ_BONUS_EPIC, FREQ_BONUS_ELITE,
                                FREQ_BONUS_HIGH, FREQ_BONUS_MID, FREQ_BONUS_LOW, FREQ_BONUS_FEW,
                                PENALTY_VISION_2, PENALTY_VISION_3, PENALTY_CTX, BONUS_VISION_VOCAB);
                        }
                    }
                }

                string ONE_TO_MANY_S2T = "㐹万丑个丰了于云亘仆仇仑价仿伙余佛佣俊修借僵克党具冢冬冲凄准凌几凶出划别刮制勋千升卜占卤卷厂历厘参发只台叶叹吁吃合吊同后向吣呆周咨咸咽哄哗唇啮喂噪回团困坐坛坝坯埙堤复夫夸夹奸姜娘娴宁它家尝尸尽局岩岳巨布帘席干并幸广庵弥弦当录彩征径御志念恤恶愈愿戚扇才扎托扣折抵拐拿挂挨挽捆捍据搜摆斗斤斫旋昆暗曲札术朱朴杆杠杯杰松板极果枪柜栗核梁棱檗欲毁汇沈沾泛注浚涂涌淀游溪滟漓澄炼烟焰熏狸玩琅璇症皂矩确硷私秋种穗筑筱签糊系累纤绱绷耇胄背胜胡脏腊腌膻致舍艳芸苏苔苹范荐荡荫药获蒙蔑藤虫蚝蜡蝎表袅裥证谥谷豆象赝赞跖辟迹适郁酸采里鉴针钟钥钫钻铲链锄锫镋镎镢镰闲雕面须饥鹇洒虱湿袜";
                string ONE_TO_MANY_T2S = "么乾仝俱像儘剋劃劄勣叚吒哩喆噁噹坏堃夥崙廬彷徵戰扞擣於昇椀椏氾沈淼澂瀋瀰牴犇甦甯畫瞭礆祇祕筦箚絜綵線耑脩菉蒐薹藉蘋衹袷襬覆託訢諫諮譾讎谿貲買迺逕邨釐鉅鍊鍾鏇鑪钁開閒阪陞靦韝頫願颺餘餬餱餵驄鵰麪麴麵麼麽齧龢";

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
                        int startOffset = cutsList[tIdx];
                        int endOffset = cutsList[tIdx + 1];
                        int rawLen = endOffset - startOffset;
                        if (rawLen <= 0) { chunksOut[tIdx] = ""; return state; }

                        // 相容字安全查表換字閘道
                        string localInput = null;
                        int start = startOffset;
                        int len = rawLen;

                        // 利用直接定址，0 開銷掃描 CJK 相容區 (排除代理對與非相容字)
                        bool hasCompat = false;
                        if (!isAlt) {
                            for (int k = startOffset; k < endOffset; k++) {
                                char c = fullInput[k];
                                // 鎖定相容字 U+F900~U+FAFF 與平面2的高位代理
                                if ((c >= 0xF900 && c <= 0xFAFF) || (c >= 0xD840 && c <= 0xD87F)) {
                                    hasCompat = true;
                                    break;
                                }
                            }
                        }

                        // 動態原地重組
                        int compatReplaceCount = 0;
                        if (hasCompat) {
                            System.Text.StringBuilder sbClean = new System.Text.StringBuilder(rawLen);
                            int k = startOffset;
                            while (k < endOffset) {
                                int cp = (int)fullInput[k];
                                int step = 1;

                                if (k + 1 < endOffset && char.IsSurrogatePair(fullInput, k)) {
                                    cp = char.ConvertToUtf32(fullInput, k);
                                    step = 2;
                                }

                                int rep = GetFastReplacement(cp);
                                if (rep != 0) {
                                    if (rep <= 0xFFFF) sbClean.Append((char)rep);
                                    else sbClean.Append(char.ConvertFromUtf32(rep));
                                    compatReplaceCount++;
                                } else {
                                    if (step == 2) {
                                        sbClean.Append(fullInput[k]);
                                        sbClean.Append(fullInput[k + 1]);
                                    } else {
                                        sbClean.Append(fullInput[k]);
                                    }
                                }
                                k += step;
                            }
                            localInput = sbClean.ToString();
                            start = 0; 
                            len = localInput.Length;
                        } else {
                            localInput = fullInput;
                            start = startOffset;
                            len = rawLen;
                        }

                        state.EnsureSize(len);
                        int i = 0, lastMatchEnd = 0;

                        bool hasReplacements = hasCompat; 
                        int localReplaceCount = compatReplaceCount;

                        double[] routeScore = state.routeScore;
                        int[] routeNext = state.routeNext;
                        TrieNode[] routeNode = state.routeNode;
                        CharWriter sb = state.sb;
                        CharWriter fbSb = state.fbSb;
                        sb.Length = 0;

                        if (isJiebaActive) {
                            // 第 1 階段：Jieba DAG + DP 路由計算
                            routeScore[len] = 0;
                            routeNext[len] = len;
                            routeNode[len] = null;

                            Action<int, int> runViterbi = (startPtr, obsLen) => {
                                if (hmm == null || obsLen == 0) {
                                    routeNext[startPtr] = startPtr + obsLen;
                                    routeNode[startPtr] = null;
                                    return;
                                }
                                state.EnsureViterbiSize(obsLen);
                                double[] V = state.vBuf;
                                int[] backPath = state.bpBuf;
                                int[] statesPath = state.spBuf;

                                int firstCharCode = (int)localInput[start + startPtr];
                                for (int s = 0; s < 4; s++) {
                                    V[s] = hmm.start_p[s] + hmm.emit_p[s * 65536 + firstCharCode];
                                }

                                for (int t = 1; t < obsLen; t++) {
                                    int charCode = (int)localInput[start + startPtr + t];
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
                                        routeNode[startPtr + begin] = null;
                                        begin = k + 1;
                                    }
                                }
                                if (begin < obsLen) {
                                    routeNext[startPtr + begin] = startPtr + obsLen;
                                    routeNode[startPtr + begin] = null;
                                }
                            };

                            for (int idx = len - 1; idx >= 0; idx--) {
                                char firstChar = localInput[start + idx];
                                if (firstChar < fastSkipLimit) {
                                    routeScore[idx] = routeScore[idx + 1];
                                    // 區塊跳躍 (反向單步)
                                    while (idx - 1 >= 0 && localInput[start + idx - 1] < fastSkipLimit) {
                                        idx--;
                                        routeScore[idx] = routeScore[idx + 1];
                                    }
                                    continue;
                                }

                                TrieNode node = rootNodes[firstChar];
                                double bestScore = double.NegativeInfinity;
                                int bestNext = idx + 1;
                                TrieNode bestNode = null;

                                if (node == null) {
                                    routeScore[idx] = -18.0 + routeScore[idx + 1];
                                    routeNext[idx] = idx + 1;
                                    routeNode[idx] = null;
                                    continue;
                                }

                                // 單字葉節點快速路徑：沒有任何子節點時，不進入通用 DAG 候選搜尋迴圈。
                                if (!node.HasChildren) {
                                    double leafPenalty = IsOneToMany[firstChar] ? PENALTY_ONE_TO_MANY : 0;
                                    routeScore[idx] = node.JiebaBaseScore + leafPenalty + routeScore[idx + 1];
                                    routeNext[idx] = idx + 1;
                                    routeNode[idx] = node;
                                    continue;
                                }

                                int j = idx;
                                TrieNode curr = node;
                                while (j < len) {
                                    if (curr.LogFreq != -18.0 || curr.Value != null || j == idx) {
                                        int wordLen = j - idx + 1;

                                        // 一對多單字懲罰機制 (直接定址優化)
                                        double penalty = 0;
                                        if (wordLen == 1 && IsOneToMany[localInput[start + idx]]) {
                                            penalty = PENALTY_ONE_TO_MANY;
                                        }

                                        // 總分計算
                                        double score = curr.JiebaBaseScore + penalty + routeScore[j + 1];
                                        if (score > bestScore) {
                                            bestScore = score;
                                            bestNext = j + 1;
                                            bestNode = curr;
                                        }
                                    }
                                    j++;
                                    if (j < len) {
                                        char nextChar = localInput[start + j];
                                        if ((curr.FastMask & (1UL << (nextChar & 63))) == 0 ||
                                            (curr.FastMask2 & (1UL << ((nextChar >> 6) & 63))) == 0) break;
                                        TrieNode nxtNode = null;
                                        curr = curr.TryGetChild(nextChar, out nxtNode) ? nxtNode : null;
                                        if (curr == null) break;
                                    }
                                }
                                routeScore[idx] = bestScore;
                                routeNext[idx] = bestNext;
                                routeNode[idx] = bestNode;
                            }

                            // 第 1.5 階段：HMM 處理連續單字
                            int ptr = 0;
                            while (ptr < len) {
                                if (localInput[start + ptr] < fastSkipLimit) {
                                    // 區塊跳躍 (正向 4 步)
                                    while (ptr + 3 < len && (localInput[start + ptr] | localInput[start + ptr + 1] | localInput[start + ptr + 2] | localInput[start + ptr + 3]) < fastSkipLimit) ptr += 4;
                                    while (ptr < len && localInput[start + ptr] < fastSkipLimit) ptr++;
                                    continue;
                                }

                                int nextPtr = routeNext[ptr];
                                int wLen = nextPtr - ptr;
                                char code = localInput[start + ptr];

                                if (wLen == 1 && code >= 0x4E00 && code <= 0x9FFF) {
                                    int startPtr = ptr;
                                    int endPtr = nextPtr;
                                    while (endPtr < len) {
                                        int nxt = routeNext[endPtr];

                                        char endChar = localInput[start + endPtr];
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
                                if (localInput[start + extractIdx] < fastSkipLimit) {
                                    // 區塊跳躍 (正向 4 步)
                                    while (extractIdx + 3 < len && (localInput[start + extractIdx] | localInput[start + extractIdx + 1] | localInput[start + extractIdx + 2] | localInput[start + extractIdx + 3]) < fastSkipLimit) extractIdx += 4;
                                    while (extractIdx < len && localInput[start + extractIdx] < fastSkipLimit) extractIdx++;
                                    continue;
                                }

                                int nxt = routeNext[extractIdx];

                                // 檢查 PhraseLogic
                                int firstCharCode = localInput[start + extractIdx];
                                int currentMaxThres = HasPhraseLogicStart[firstCharCode] ? MaxPhraseThreshold[firstCharCode] : 4;

                                if ((nxt - extractIdx) < currentMaxThres && isCtx && !isShift && HasPhraseLogicStart[firstCharCode]) {
                                    string pTarget; int pLen;
                                    if (TryApplyPhraseLogic(extractIdx, start, len, localInput, ref localReplaceCount, out pTarget, out pLen)) {
                                        hasReplacements = true;
                                        if (extractIdx > lastMatchEnd) sb.Append(localInput, start + lastMatchEnd, extractIdx - lastMatchEnd);
                                        sb.Append(pTarget);
                                        extractIdx += pLen;
                                        lastMatchEnd = extractIdx;
                                        continue;
                                    }
                                }

                                // 🗡️ 真・視界邏輯介入 (零分配加速)
                                int _wLen = nxt - extractIdx;

                                // 用 IsAnchorStart 擋掉不是錨點的詞
                                if (VisionAnchors != null && VisionAnchors.Count > 0 && _wLen > 1 && IsAnchorStart[localInput[start + extractIdx]]) 
                                {
                                    // 第一段拔刀：偵測到定錨點 (純字典樹判定，0 Allocation)
                                    bool isAnchor = false;
                                    TrieNode aNode = rootNodes[localInput[start + extractIdx]];
                                    if (aNode != null) {
                                        for (int k = 1; k < _wLen; k++) {
                                            TrieNode nextN = null;
                                            if (aNode.TryGetChild(localInput[start + extractIdx + k], out nextN)) {
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
                                        TrieNode continuousNode = rootNodes[localInput[start + visionIdx]];

                                        while (vk < len && (vk - visionIdx) <= 8)
                                        {
                                            if (continuousNode != null) 
                                            {
                                                TrieNode nextN = null;
                                                if (continuousNode.TryGetChild(localInput[start + vk], out nextN)) {
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
                                            TrieNode sNode = rootNodes[localInput[start + stabIdx]];

                                            while (sk < len && (sk - stabIdx) <= 8)
                                            {
                                                if (sNode != null) 
                                                {
                                                    TrieNode nextSN = null;
                                                    if (sNode.TryGetChild(localInput[start + sk], out nextSN)) 
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
                                char firstChar2 = localInput[start + extractIdx];

                                string openccTarget = null;
                                TrieNode node2 = rootNodes[firstChar2];
                                TrieNode cachedNode = routeNext[extractIdx] == nxt ? routeNode[extractIdx] : null;

                                if (wordLen > 1 && cachedNode != null && cachedNode.Value != null) {
                                    openccTarget = cachedNode.Value;
                                } else if (node2 != null) {
                                    if (wordLen == 1) {
                                        if (isCtx && !isShift && LogicDelegate != null && HasContextLogic[firstChar2]) {
                                            string logicResult = LogicDelegate((int)firstChar2, start + extractIdx, localInput);
                                            if (logicResult != null) openccTarget = applyTWVariants(logicResult);
                                        }
                                        if (openccTarget == null && node2.Value != null) {
                                            openccTarget = node2.Value;
                                        }
                                    } else {
                                        TrieNode currNode = node2;
                                        for (int k = 1; k < wordLen; k++) {
                                            TrieNode nextN;
                                            if (currNode.TryGetChild(localInput[start + extractIdx + k], out nextN)) {
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
                                } else if (wordLen > 1) {
                                    // 單字分詞在前面的直接路徑已完成 ContextLogic 與 node2.Value 檢查。
                                    // openccTarget 仍為 null 時，略過只會重複相同工作的單字 Fallback。
                                    int subK = 0;
                                    fbSb.Length = 0;

                                    while (subK < wordLen) {
                                        char ch = localInput[start + extractIdx + subK];
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
                                                char nextChar = localInput[start + extractIdx + subK + scan];
                                                if ((temp.FastMask & (1UL << (nextChar & 63))) == 0 ||
                                                    (temp.FastMask2 & (1UL << ((nextChar >> 6) & 63))) == 0) break;
                                                TrieNode nextN;
                                                if (!temp.TryGetChild(nextChar, out nextN)) break;
                                                temp = nextN;
                                                if (temp.Value != null) {
                                                    longestMatchLen = scan + 1;
                                                    longestMatchTarget = temp.Value;
                                                }
                                                scan++;
                                            }
                                        }

                                        if (longestMatchLen <= 1 && isCtx && !isShift && LogicDelegate != null && HasContextLogic[ch]) {
                                            string lRes = LogicDelegate((int)ch, start + extractIdx + subK, localInput);
                                            if (lRes != null) {
                                                longestMatchTarget = applyTWVariants(lRes);
                                                longestMatchLen = 1;
                                            }
                                        }

                                        int matchLen2 = Math.Max(1, longestMatchLen);

                                        if (longestMatchTarget != null) {
                                            if (fbSb.Length == 0 && subK > 0) fbSb.Append(localInput, start + extractIdx, subK);
                                            fbSb.Append(longestMatchTarget);
                                        } else if (fbSb.Length > 0) {
                                            fbSb.Append(ch);
                                        }

                                        subK += matchLen2;
                                    }
                                    if (fbSb.Length > 0) finalTarget = fbSb.ToString();
                                }

                                // 零分配比較邏輯
                                if (finalTarget != null) {
                                    bool isChanged = false;
                                    if (finalTarget.Length != wordLen) isChanged = true;
                                    else { 
                                        for (int k = 0; k < wordLen; k++) {
                                            if (finalTarget[k] != localInput[start + extractIdx + k]) { isChanged = true; break; } 
                                        } 
                                    }

                                    if (isChanged) {
                                        hasReplacements = true;
                                        localReplaceCount++;
                                        if (extractIdx > lastMatchEnd) sb.Append(localInput, start + lastMatchEnd, extractIdx - lastMatchEnd);
                                        sb.Append(finalTarget);
                                        lastMatchEnd = nxt;
                                    }
                                }
                                extractIdx = nxt;
                            }

                        } else {
                            // 原本的貪婪最長匹配轉換 (非結巴模式)
                            while (i < len) {
                                while (i + 3 < len && (localInput[start + i] | localInput[start + i + 1] | localInput[start + i + 2] | localInput[start + i + 3]) < fastSkipLimit) i += 4;
                                while (i < len && localInput[start + i] < fastSkipLimit) i++; if (i >= len) break; 
                                char firstChar = localInput[start + i];

                                TrieNode node = rootNodes[firstChar];
                                int longestMatchLen = 0; string longestMatchTarget = null;

                                if (node != null) {
                                    if (node.Value != null) { longestMatchLen = 1; longestMatchTarget = node.Value; }
                                    int j = i + 1; TrieNode curr = node;
                                    while (j < len) {
                                        char nextChar = localInput[start + j];
                                        if ((curr.FastMask & (1UL << (nextChar & 63))) == 0 ||
                                            (curr.FastMask2 & (1UL << ((nextChar >> 6) & 63))) == 0) break;
                                        TrieNode nextNode;
                                        if (!curr.TryGetChild(nextChar, out nextNode)) break;
                                        curr = nextNode;
                                        if (curr.Value != null) { longestMatchLen = j - i + 1; longestMatchTarget = curr.Value; }
                                        j++;
                                    }
                                }

                                // 檢查 PhraseLogic
                                int currentMaxThres = HasPhraseLogicStart[firstChar] ? MaxPhraseThreshold[firstChar] : 4;
                                if (longestMatchLen < currentMaxThres && isCtx && !isShift && HasPhraseLogicStart[firstChar]) {
                                    string pTarget; int pLen;
                                    if (TryApplyPhraseLogic(i, start, len, localInput, ref localReplaceCount, out pTarget, out pLen)) {
                                        hasReplacements = true;
                                        if (i > lastMatchEnd) sb.Append(localInput, start + lastMatchEnd, i - lastMatchEnd);
                                        sb.Append(pTarget);
                                        i += pLen;
                                        lastMatchEnd = i;
                                        continue;
                                    }
                                }

                                // 🔭 視界邏輯 (0 Allocation 極速版)
                                if (isVis && !isShift && longestMatchLen > 1) {
                                    int foundALen = 0;
                                    TrieNode aN = rootNodes[localInput[start + i]];
                                    if (aN != null) {
                                        if (aN.IsVisionAnchor) foundALen = 1;
                                        // 往前探勘
                                        for (int k = 1; k < longestMatchLen; k++) {
                                            if (aN.TryGetChild(localInput[start + i + k], out aN)) {
                                                if (aN.IsVisionAnchor) foundALen = k + 1;
                                            } else break;
                                        }
                                    }

                                    if (foundALen > 1 && longestMatchLen >= 4 && longestMatchLen > foundALen) foundALen = 0;

                                    if (foundALen > 1) {
                                        int vIdx = i + foundALen - 1; 
                                        int vWordLen = 0;
                                        int vk = vIdx + 1; 
                                        TrieNode vN = rootNodes[localInput[start + vIdx]];

                                        while (vk < len) {
                                            if (vN != null) {
                                                TrieNode nextVN;
                                                if (vN.TryGetChild(localInput[start + vk], out nextVN)) vN = nextVN; else vN = null;
                                            } else vN = null;

                                            int currentSubLen = vk - vIdx + 1;

                                            if (vN != null && ((vN.Value != null) || vN.IsVisionVocab)) vWordLen = currentSubLen; 

                                            if (vN == null && currentSubLen > 6) break;
                                            vk++;
                                        }

                                        if (vWordLen > 0) {
                                            bool stable = true; 
                                            int dIdx = vIdx + vWordLen - 1; 
                                            int dk = dIdx + 1; 
                                            TrieNode dN = rootNodes[localInput[start + dIdx]];

                                            while (dk < len) {
                                                if (dN != null) {
                                                    TrieNode nextDN;
                                                    if (dN.TryGetChild(localInput[start + dk], out nextDN)) dN = nextDN; else dN = null;
                                                } else dN = null;

                                                int currentSubLen = dk - dIdx + 1;

                                                if (dN != null && ((dN.Value != null) || dN.IsVisionVocab)) { stable = false; break; } 

                                                if (dN == null && currentSubLen > 6) break;
                                                dk++;
                                            }

                                            if (stable) {
                                                int bLen = 1; 
                                                TrieNode bN = rootNodes[localInput[start + i]];
                                                if (bN != null) {
                                                    // 只找尋小於 foundALen - 1 的次長 Anchor
                                                    for (int k = 1; k < foundALen - 1; k++) {
                                                        if (bN.TryGetChild(localInput[start + i + k], out bN)) {
                                                            if (bN.IsVisionAnchor) bLen = k + 1;
                                                        } else break;
                                                    }
                                                }

                                                longestMatchLen = bLen < 2 ? 1 : bLen;

                                                // 重新定位 Target
                                                TrieNode tN = rootNodes[localInput[start + i]]; 
                                                for (int k = 1; k < longestMatchLen; k++) {
                                                    if (tN.TryGetChild(localInput[start + i + k], out tN)) {} else { tN = null; break; }
                                                }
                                                longestMatchTarget = tN != null ? tN.Value : null;
                                            }
                                        }
                                    }
                                }

                                // ⚓ 語法邏輯 (精簡變數與極速判定版)
                                if (isCtx && !isShift && longestMatchLen <= 1 && LogicDelegate != null && HasContextLogic[firstChar]) {
                                    string lRes = LogicDelegate((int)firstChar, start + i, localInput);
                                    if (lRes != null) { 
                                        longestMatchTarget = applyTWVariants(lRes); 
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
                                        isChanged = longestMatchTarget[0] != localInput[start + i];
                                    } else {
                                        // 只有長度大於 1 的詞彙，才會進入迴圈
                                        for (int k = 0; k < targetLen; k++) {
                                            if (longestMatchTarget[k] != localInput[start + i + k]) { 
                                                isChanged = true; 
                                                break; 
                                            }
                                        }
                                    }

                                    if (isChanged) {
                                        hasReplacements = true;
                                        localReplaceCount++;
                                        if (i > lastMatchEnd) sb.Append(localInput, start + lastMatchEnd, i - lastMatchEnd);
                                        sb.Append(longestMatchTarget);
                                        lastMatchEnd = i + matchLen;
                                    } 
                                    i += matchLen;
                                } else {
                                    i++;
                                }
                            }
                        }

                        // 結算未轉換文字
                        if (!hasReplacements) {
                            chunksOut[tIdx] = localInput.Substring(start, len);
                        } else {
                            if (lastMatchEnd < len) sb.Append(localInput, start + lastMatchEnd, len - lastMatchEnd);
                            chunksOut[tIdx] = sb.ToString();
                        }
                        chunkReplaceCounts[tIdx] = localReplaceCount;

                        return state; 
                    },

                    (ThreadState state) => {
                        if (state != null) {
                            if (state.sb != null) state.sb.Length = 0;
                            if (state.fbSb != null) state.fbSb.Length = 0;
                        }
                    }
                );

                for (int i = 0; i < chunkReplaceCounts.Length; i++) {
                    totalReplaceCount += chunkReplaceCounts[i];
                }

                NativeSetClipboardChunks(chunksOut);
                return totalReplaceCount;

            } catch (Exception ex) { 
                if (macroPathOrCmd != "LOAD_DICT") {
                    MessageBox.Show("Execution Error: " + ex.Message + "\n" + ex.StackTrace); 
                }
                Environment.Exit(-1);
                return 0; 
            }
        }

        // 微型規則解譯器
        static void CompileContextLogic(string jsSource, string mode, JseeStructureExtractor extractor) {
            FastContextRules.Clear();
            Array.Clear(FastContextRuleArray, 0, FastContextRuleArray.Length);
            Array.Clear(HasContextLogic, 0, HasContextLogic.Length);

            string mVal;
            if (!extractor.TryGetConstString("m", out mVal)) {
                Match legacyM = Regex.Match(jsSource, @"const\s+m\s*=\s*[""'](.*?)[""'];");
                mVal = legacyM.Success ? legacyM.Groups[1].Value : "";
            }

            string sn2Value;
            if (!extractor.TryGetConstString("sn2", out sn2Value)) {
                Match legacySn2 = Regex.Match(jsSource, @"const\s+sn2\s*=\s*[""'](.*?)[""'];");
                sn2Value = legacySn2.Success ? legacySn2.Groups[1].Value : "";
            }
            HashSet<uint> sn2Set = new HashSet<uint>();
            if (!string.IsNullOrEmpty(sn2Value)) {
                string[] sn2Words = sn2Value.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string w in sn2Words) {
                    if (w.Length == 2) sn2Set.Add(((uint)w[0] << 16) | w[1]);
                }
            }

            string contextSource;
            if (!extractor.TryGetConstObjectBody("ContextLogicData", out contextSource)) {
                contextSource = jsSource;
            }

            var entryMatches = Regex.Matches(contextSource, @"[""']?(?<id>0x[0-9A-Fa-f]+|[^""'\s\[\]:,])[""']?\s*:\s*\[(?<rules>.*?)\]\s*(?=[,}])", RegexOptions.Singleline);

            foreach (Match entry in entryMatches) {
                string idStr = entry.Groups["id"].Value;
                char anchorChar = idStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 
                                  (char)System.Convert.ToInt32(idStr.Substring(2), 16) : idStr[0];
                string rulesRaw = entry.Groups["rules"].Value;

                var ruleMatches = Regex.Matches(rulesRaw, @"[""'](?<rule>[^""']+)[""']");
                List<ContextRule> orderedRules = new List<ContextRule>();

                foreach (Match rm in ruleMatches) {
                    string line = rm.Groups["rule"].Value.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    int arrowIdx = line.IndexOf("=>");
                    if (arrowIdx == -1) continue;

                    string conditionPart = line.Substring(0, arrowIdx).Trim();
                    string targetPart = line.Substring(arrowIdx + 2).Trim();

                    ContextRule rule = new ContextRule();
                    rule.Target = targetPart;

                    if (conditionPart.StartsWith("default")) {
                        orderedRules.Add(rule);
                        continue;
                    }

                    string[] condParts = conditionPart.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string condStr in condParts) {
                        ContextCondition cond = new ContextCondition();
                        string temp = condStr.Trim();

                        if (temp.StartsWith("!")) { cond.IsExclude = true; temp = temp.Substring(1); }

                        int colonIdx = temp.IndexOf(':');
                        if (colonIdx == -1) continue;

                        string offsetStr = temp.Substring(0, colonIdx).Trim();
                        cond.CharSet = temp.Substring(colonIdx + 1).Trim();

                        if (offsetStr.StartsWith("+")) offsetStr = offsetStr.Substring(1);
                        int.TryParse(offsetStr, out cond.Offset);

                        if (cond.CharSet == "@sn2") {
                            cond.IsSn2 = true;
                        } else {
                            cond.CharSet = cond.CharSet.Replace("@m", mVal).Replace("@s", " \r\n");
                        }

                        rule.Conditions.Add(cond);
                    }
                    orderedRules.Add(rule);
                }

                if (orderedRules.Count > 0) {
                    ContextRule[] ruleArray = orderedRules.ToArray();
                    for (int rr = 0; rr < ruleArray.Length; rr++) {
                        ruleArray[rr].ConditionArray = ruleArray[rr].Conditions.ToArray();
                    }
                    FastContextRules[anchorChar] = orderedRules;
                    FastContextRuleArray[anchorChar] = ruleArray;
                    HasContextLogic[anchorChar] = true;
                }
            }

            LogicDelegate = (int c, int idx, string text) => {
                ContextRule[] rules = FastContextRuleArray[(char)c];
                if (rules == null) return null;

                for (int r = 0; r < rules.Length; r++) {
                    ContextRule rule = rules[r];
                    ContextCondition[] conds = rule.ConditionArray;
                    bool isMatch = true;

                    for (int i = 0; i < conds.Length; i++) {
                        ContextCondition cond = conds[i];
                        int targetIdx = idx + cond.Offset;

                        if (cond.IsSn2) {
                            bool exists = false;
                            if (targetIdx >= 0 && targetIdx + 1 < text.Length) {
                                uint key = ((uint)text[targetIdx] << 16) | text[targetIdx + 1];
                                if (sn2Set.Contains(key)) exists = true;
                            }
                            if (cond.IsExclude) { if (exists) { isMatch = false; break; } }
                            else { if (!exists) { isMatch = false; break; } }
                        } else {
                            char testChar = (targetIdx >= 0 && targetIdx < text.Length) ? text[targetIdx] : ' ';
                            string charSet = cond.CharSet;
                            bool charExists = false;
                            for (int k = 0; k < charSet.Length; k++) {
                                if (charSet[k] == testChar) { charExists = true; break; }
                            }
                            if (cond.IsExclude) { if (charExists) { isMatch = false; break; } }
                            else { if (!charExists) { isMatch = false; break; } }
                        }
                    }
                    if (isMatch) return rule.Target;
                }
                return null;
            };
        }

        static bool TryGetJiebaLogFreq(
            string source,
            int start,
            int length,
            double logTotal,
            Dictionary<int, double> cache,
            out double logFreq) {

            logFreq = 0;
            if (length <= 0) return false;

            int value = 0;
            bool isInteger = true;
            for (int i = 0; i < length; i++) {
                char c = source[start + i];
                if (c < '0' || c > '9') {
                    isInteger = false;
                    break;
                }

                int digit = c - '0';
                if (value > (int.MaxValue - digit) / 10) {
                    isInteger = false;
                    break;
                }
                value = value * 10 + digit;
            }

            if (isInteger && value > 0) {
                if (!cache.TryGetValue(value, out logFreq)) {
                    logFreq = Math.Log((double)value) - logTotal;
                    cache[value] = logFreq;
                }
                return true;
            }

            string freqText = source.Substring(start, length);
            double frequency;
            if (!double.TryParse(freqText, out frequency)) return false;
            logFreq = Math.Log(frequency) - logTotal;
            return true;
        }

        static void AddJiebaWord(
            TrieNode[] roots,
            string source,
            int wordStart,
            int wordLength,
            double logFreq) {

            if (wordLength <= 0) return;

            char firstChar = source[wordStart];
            if (roots[firstChar] == null) roots[firstChar] = new TrieNode();
            TrieNode current = roots[firstChar];

            int wordEnd = wordStart + wordLength;
            for (int i = wordStart + 1; i < wordEnd; i++) {
                char c = source[i];
                current.FastMask |= 1UL << (c & 63);
                current.FastMask2 |= 1UL << ((c >> 6) & 63);
                current = current.GetOrAddChild(c);
            }

            current.LogFreq = logFreq;
        }

        static void AddWord(TrieNode[] roots, string k, string v, double logFreq = -18.0, byte flag = 0) {
            if (string.IsNullOrEmpty(k)) return;
            char f = k[0]; if (roots[f] == null) roots[f] = new TrieNode();
            TrieNode c = roots[f];
            for (int i = 1; i < k.Length; i++) {
                char nextChar = k[i];
                c.FastMask |= 1UL << (nextChar & 63);
                c.FastMask2 |= 1UL << ((nextChar >> 6) & 63);
                c = c.GetOrAddChild(nextChar);
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

        static string ExtractBlock(string src, string name, JseeStructureExtractor extractor) {
            string value;
            if (extractor.TryGetConstTemplate(name, out value)) return value;
            Match legacyMatch = Regex.Match(src, name + @"\s*=\s*[`](?<c>[\s\S]*?)[`]", RegexOptions.Singleline);
            return legacyMatch.Success ? legacyMatch.Groups["c"].Value : "";
        }

        static HashSet<string> ExtractSet(string src, string name, JseeStructureExtractor extractor) {
            var set = new HashSet<string>();
            string body;
            if (!extractor.TryGetConstSetBody(name, out body)) {
                Match legacyMatch = Regex.Match(src, name + @"\s*=\s*new\s*Set\(\[\s*(.*?)\s*\]\)", RegexOptions.Singleline);
                if (!legacyMatch.Success) return set;
                body = legacyMatch.Groups[1].Value;
            }
            foreach (Match item in Regex.Matches(body, @"""(.*?)""|'(.*?)'")) {
                set.Add(item.Groups[1].Value + item.Groups[2].Value);
            }
            return set;
        }
    }
}