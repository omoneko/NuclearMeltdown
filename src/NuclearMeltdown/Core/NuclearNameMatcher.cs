using System;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// 建物名が「原子力発電所」を示すかを、名称キーワードで判定する純粋ロジック。
    /// バニラの "Nuclear Power Plant" に加え、Workshop アセット
    /// （例: "Chernobyl NPP Units 3-4"）のように "NPP" 等を含む名称も拾う。
    /// 大文字小文字は区別しない。ゲーム型に依存しないので単体テスト可能。
    /// </summary>
    public static class NuclearNameMatcher
    {
        /// <summary>
        /// 既定の判定キーワード。名称にいずれかを含めば原発とみなす。
        /// AI 種別（PowerPlantAI）の絞り込みは呼び出し側で行う前提。
        /// </summary>
        public static readonly string[] DefaultKeywords =
        {
            "Nuclear",  // バニラ "Nuclear Power Plant"
            "NPP",      // Nuclear Power Plant の略。Workshop 原発アセットで多用
            "Reactor",  // 原子炉系アセット
            "Atom",     // "Atomic ..." 系
            "原子力",
            "原発",
        };

        /// <summary>既定キーワードで判定する。</summary>
        public static bool Matches(string name)
        {
            return Matches(name, DefaultKeywords);
        }

        /// <summary>指定キーワード（いずれか一致で true）で判定する。大文字小文字無視。</summary>
        public static bool Matches(string name, string[] keywords)
        {
            if (string.IsNullOrEmpty(name) || keywords == null) return false;
            for (int i = 0; i < keywords.Length; i++)
            {
                string kw = keywords[i];
                if (!string.IsNullOrEmpty(kw) &&
                    name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
