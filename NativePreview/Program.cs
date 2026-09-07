using System;
using System.IO;
using System.Windows.Forms;

namespace Hearthhold.Preview
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length >= 2 && args[0] == "--render")
                {
                    using (GameWindow window = new GameWindow(true))
                    {
                        if (args.Length > 2 && args[2] == "battle") window.PrepareBattlePreview();
                        window.RenderToFile(Path.GetFullPath(args[1]));
                    }
                    return;
                }
                Application.Run(new GameWindow(false));
            }
            catch (Exception ex)
            {
                string crash = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hearthhold-error.txt");
                try { File.WriteAllText(crash, ex.ToString()); } catch { }
                if (args.Length == 0) MessageBox.Show(ex.Message + "\n\n详细信息：" + crash, "篝火堡垒", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
        }
    }
}
