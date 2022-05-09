using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Drawing;

namespace SecDrvChangeShell
{
    internal class SelectionForm : Form
    {
        Label lbText;
        Timer timer;
        String exec_program;
        bool full_auto;

        public SelectionForm()
        {
            SetFormProps();
            ChangeSecDrvValidate();

        }

        private void SetFormProps()
        {
            this.Width = 100;
            this.Height = 40;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;

            lbText = new Label();
            lbText.Top = 0;
            lbText.Left = 0;
            lbText.Text = "";
            lbText.Height = this.Height;
            lbText.Width = this.Width;
            lbText.TextAlign = ContentAlignment.MiddleCenter;

            this.Controls.Add(lbText);

            this.Shown += Form_Shown;
        }


        private void ChangeSecDrvValidate()
        {
            exec_program = "";
            full_auto = false;

            //コマンドライン引数を配列で取得する
            string[] cmds = System.Environment.GetCommandLineArgs();

            // onという引数を含むか
            if (Array.IndexOf(cmds, "auto") > 0)
            {
                full_auto = true;
            }

            // onという引数を含むか
            if (Array.IndexOf(cmds, "on") > 0)
            {
                // 有効とはいえなければ
                if (!IsValid())
                {
                    // 有効指定
                    DoValid();
                }
                return;
            }

            // offという引数を含むか
            else if (Array.IndexOf(cmds, "off") > 0)
            {
                // 無効とは言えなければ
                if (!IsInvalid())
                {
                    DoInvalid();
                }
                return;
            }

            // どうもファイルを実行しようとしているぞ
            for (int i = 1; i < cmds.Length; i++)
            {
                {
                    if (File.Exists(cmds[i]))
                    {
                        if (!IsValid())
                        {
                            DoValid();
                        }

                        exec_program = cmds[i];
                        return;
                    }
                }
            }

            // 現在有効ならば
            if (IsValid())
            {
                // 無効に
                DoInvalid();
                return;
            }
            // そうでないなら
            else
            {
                // 有効に
                DoValid();
                return;
            }
        }

        public void Form_Shown(Object o, EventArgs e)
        {
            timer = new Timer();
            timer.Tick += new EventHandler(Form_Close);
            // 何か処理をしたようだ
            if (lbText.Text != "")
            {
                timer.Interval = 1500;
            }
            else
            {
                this.Hide();
                timer.Interval = 50;
            }
            timer.Enabled = true;
        }

        public void Form_Close(Object o, EventArgs e)
        {
            timer.Stop();

            if (exec_program != "")
            {
                // 外部プログラムを実行
                System.Diagnostics.Process p = System.Diagnostics.Process.Start(exec_program);

                // フルオートを選択していたら、
                if (full_auto)
                {
                    this.Hide();
                    p.WaitForExit();

                    lbText.Text = "";
                    this.Show();
                    DoInvalid();

                    exec_program = "";
                    full_auto = false;
                    timer.Interval = 1500;
                    timer.Start();

                }
                else
                {
                    this.Close();
                }
            }
            else
            {
                this.Close();
            }
        }

        public bool IsValid()
        {
            List<Match> match_array = new List<Match>();
            // 現在どういう状況
            var running_status = doConsoleCommand("sc query secdrv");

            var r1 = new Regex(@"RUNNING", RegexOptions.Multiline);
            var m1 = r1.Match(running_status);

            var service_status = doConsoleCommand("sc qc secdrv");
            var r2 = new Regex(@"DEMAND_START", RegexOptions.Multiline);
            var m2 = r2.Match(service_status);

            return m1.Success && m2.Success;
        }

        public bool IsInvalid()
        {
            List<Match> match_array = new List<Match>();
            // 現在どういう状況
            var running_status = doConsoleCommand("sc query secdrv");

            var r1 = new Regex(@"STOPPED", RegexOptions.Multiline);
            var m1 = r1.Match(running_status);

            var service_status = doConsoleCommand("sc qc secdrv");
            var r2 = new Regex(@"DISABLED", RegexOptions.Multiline);
            var m2 = r2.Match(service_status);

            return m1.Success || m2.Success;
        }

        private void DoValid()
        {
            var text = doConsoleCommand("sc query secdrv");
            doConsoleCommand("sc config secdrv start= demand");
            doConsoleCommand("sc start secdrv");

            if (IsValid())
            {
                lbText.Text = "secdrv.sys\n有効化";
            }
            else
            {
                lbText.Text = "secdrv.sys\n有効化に失敗";

                System.OperatingSystem os = System.Environment.OSVersion;
                if (os.Version.Major >= 10)
                {
                    System.Management.ManagementClass mc = new System.Management.ManagementClass("Win32_OperatingSystem");
                    System.Management.ManagementObjectCollection moc = mc.GetInstances();
                    String osName = "";
                    foreach (System.Management.ManagementObject mo in moc)
                    {
                        //簡単な説明（Windows 8.1では「Microsoft Windows 8.1 Pro」等）
                        osName = mo["Caption"].ToString();
                        break;
                    }


                    MessageBox.Show("お使いのOSは「" + osName + "」です。\n" + "このバージョンのOSでは、secdrv.sysの利用が認められていません。\n\nどうしても利用したい場合は、secdrv.sysにデジタル自己署名を行い、\nWindowsを「テストモード」にて起動してください。\n");

                    // 標準のブラウザで開いて表示する
                    System.Diagnostics.Process.Start("http://天翔記.jp/?page=nobu_mod_the_digital_sign_index");
                }
            }
        }

        private void DoInvalid()
        {
            // 無効にする。
            doConsoleCommand("sc stop secdrv");
            doConsoleCommand("sc config secdrv start= disabled");

            if (IsInvalid())
            {
                lbText.Text = "secdrv.sys\n無効化";
            }
            else
            {
                lbText.Text = "secdrv.sys\n無効化に失敗";
            }

        }

        private string doConsoleCommand(string cmd)
        {
            string path;
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                path = "Sysnative\\cmd.exe";
            }
            else
            {
                path = "System32\\cmd.exe";
            }
            string fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path);
            Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = "/c " + cmd;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.Verb = "RunAs";
            process.Start();
            string text = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            process.Close();

            return text;
        }

    }
}
