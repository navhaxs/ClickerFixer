using System.Collections.Generic;

namespace ClickerFixer.Satellite
{
    public static class LinuxToWindowsKeyCode
    {
        private static readonly Dictionary<int, int> LinuxToWindowsMap = new Dictionary<int, int>
        {
            { 1, 27 },    // ESC
            { 2, 49 },    // 1
            { 3, 50 },    // 2
            { 4, 51 },    // 3
            { 5, 52 },    // 4
            { 6, 53 },    // 5
            { 7, 54 },    // 6
            { 8, 55 },    // 7
            { 9, 56 },    // 8
            { 10, 57 },   // 9
            { 11, 48 },   // 0
            { 12, 189 },  // -
            { 13, 187 },  // =
            { 14, 8 },    // BACKSPACE
            { 15, 9 },    // TAB
            { 16, 81 },   // Q
            { 17, 87 },   // W
            { 18, 69 },   // E
            { 19, 82 },   // R
            { 20, 84 },   // T
            { 21, 89 },   // Y
            { 22, 85 },   // U
            { 23, 73 },   // I
            { 24, 79 },   // O
            { 25, 80 },   // P
            { 26, 219 },  // [
            { 27, 221 },  // ]
            { 28, 13 },   // ENTER
            { 29, 17 },   // LEFT CTRL
            { 30, 65 },   // A
            { 31, 83 },   // S
            { 32, 68 },   // D
            { 33, 70 },   // F
            { 34, 71 },   // G
            { 35, 72 },   // H
            { 36, 74 },   // J
            { 37, 75 },   // K
            { 38, 76 },   // L
            { 39, 186 },  // ;
            { 40, 222 },  // '
            { 41, 192 },  // `
            { 42, 16 },   // LEFT SHIFT
            { 43, 220 },  // \
            { 44, 90 },   // Z
            { 45, 88 },   // X
            { 46, 67 },   // C
            { 47, 86 },   // V
            { 48, 66 },   // B
            { 49, 78 },   // N
            { 50, 77 },   // M
            { 51, 188 },  // ,
            { 52, 190 },  // .
            { 53, 191 },  // /
            { 54, 16 },   // RIGHT SHIFT
            { 55, 106 },  // * (numpad)
            { 56, 18 },   // LEFT ALT
            { 57, 32 },   // SPACE
            { 58, 20 },   // CAPS LOCK
            { 59, 112 },  // F1
            { 60, 113 },  // F2
            { 61, 114 },  // F3
            { 62, 115 },  // F4
            { 63, 116 },  // F5
            { 64, 117 },  // F6
            { 65, 118 },  // F7
            { 66, 119 },  // F8
            { 67, 120 },  // F9
            { 68, 121 },  // F10
            { 69, 144 },  // NUM LOCK
            { 70, 145 },  // SCROLL LOCK
            { 71, 103 },  // 7 (numpad)
            { 72, 104 },  // 8 (numpad)
            { 73, 105 },  // 9 (numpad)
            { 74, 109 },  // - (numpad)
            { 75, 100 },  // 4 (numpad)
            { 76, 101 },  // 5 (numpad)
            { 77, 102 },  // 6 (numpad)
            { 78, 107 },  // + (numpad)
            { 79, 97 },   // 1 (numpad)
            { 80, 98 },   // 2 (numpad)
            { 81, 99 },   // 3 (numpad)
            { 82, 96 },   // 0 (numpad)
            { 83, 110 },  // . (numpad)
            { 87, 122 },  // F11
            { 88, 123 },  // F12
            { 96, 13 },   // ENTER (numpad)
            { 97, 17 },   // RIGHT CTRL
            { 98, 111 },  // / (numpad)
            { 99, 44 },   // PRINT SCREEN
            { 100, 18 },  // RIGHT ALT
            { 102, 36 },  // HOME
            { 103, 38 },  // UP ARROW
            { 104, 33 },  // PAGE UP
            { 105, 37 },  // LEFT ARROW
            { 106, 39 },  // RIGHT ARROW
            { 107, 35 },  // END
            { 108, 40 },  // DOWN ARROW
            { 109, 34 },  // PAGE DOWN
            { 110, 45 },  // INSERT
            { 111, 46 },  // DELETE
            { 119, 19 },  // PAUSE
            { 125, 91 },  // LEFT WINDOWS
            { 126, 92 },  // RIGHT WINDOWS
            { 127, 93 },  // MENU
            { 113, 173 }, // MUTE
            { 114, 174 }, // VOLUME DOWN
            { 115, 175 }, // VOLUME UP
            { 164, 179 }, // MEDIA PLAY/PAUSE
            { 165, 177 }, // MEDIA PREVIOUS
            { 166, 176 }, // MEDIA NEXT
            { 167, 178 }, // MEDIA STOP
        };

        private static readonly Dictionary<int, int> WindowsToLinuxMap;

        static LinuxToWindowsKeyCode()
        {
            WindowsToLinuxMap = new Dictionary<int, int>();
            foreach (var kvp in LinuxToWindowsMap)
            {
                WindowsToLinuxMap[kvp.Value] = kvp.Key;
            }
        }

        public static int LinuxToWindows(int linuxKeyCode)
        {
            return LinuxToWindowsMap.TryGetValue(linuxKeyCode, out int windowsKeyCode) ? windowsKeyCode : -1;
        }

        public static int WindowsToLinux(int windowsKeyCode)
        {
            return WindowsToLinuxMap.TryGetValue(windowsKeyCode, out int linuxKeyCode) ? linuxKeyCode : -1;
        }
    }
}
