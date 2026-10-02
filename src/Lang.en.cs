using System.Collections.Generic;

namespace DeskStudy
{
    // English interface text, keyed by the Chinese original. Anything missing here shows in Chinese.
    public static partial class Lang
    {
        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            // Language card in the settings center (the card itself is always bilingual).
            { "界面语言 · Language", "Language · 界面语言" },
            { "界面语言", "Language" },
            { "切换语言后需要重启程序 · Restart DeskStudy to apply", "Restart DeskStudy to apply · 切换语言后需要重启程序" },
            { "立即重启 · Restart now", "Restart now · 立即重启" },
            { "重启后切换为所选语言 · The new language applies after a restart", "The new language applies after a restart · 重启后切换为所选语言" },
        };
    }
}
