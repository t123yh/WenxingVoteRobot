﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace WenxingVoteRobot
{
    public partial class MainForm : Form
    {
        const string CaptchaUri = "http://www.chinawenxing.com.cn/inc/code.php";
        const string PageUri = "http://www.chinawenxing.com.cn/list.php?act=shownews&id=16&nid={0}";
        const string VoteUri = "http://www.chinawenxing.com.cn/list.php?act=shownews&id=16&nid={0}&yzimg={1}&tuijianccc=%E6%8E%A8%E8%8D%90";

        public MainForm()
        {
            InitializeComponent();
            lb_Counter.Alignment = ToolStripItemAlignment.Right;
        }

        Random rnd = new Random();
        bool stopFlag = false;
        int voteCount = 0;

        private void UpdateProgress()
        {
            lb_Counter.Text = string.Format("已刷 {0} 票", voteCount);
            pgb_Progress.Value = (int)(voteCount / (double)num_MaxVote.Value * 10000.0D);
        }

        private async Task Delay()
        {
            if (cb_Delay.Checked)
            {
                await Task.Delay(TimeSpan.FromMilliseconds((double)num_Delay.Value));
            }
        }

        private async Task<string> GetCaptcha(CookieContainer cookies, string userAgent)
        {

            lb_Operation.Text = "正在获取验证码图像...";
            Bitmap captcha = await Utility.HTTPGetPngAsync(userAgent, CaptchaUri, cookies);

            await Delay();

            using (CaptchaInput inputWindow = new CaptchaInput(captcha))
            {
                if(inputWindow.ShowDialog(this) == DialogResult.OK)
                {
                    return inputWindow.Captcha;
                }
                else
                {
                    throw new OperationCanceledException();
                }
            }
        }

        private async Task SimulateAccess(CookieContainer cookies, string userAgent, int id)
        {
            int simulateAccess = rnd.Next(1, 3);
            for (int i = 0; i < simulateAccess; i++)
            {
                lb_Operation.Text = string.Format("正在模拟访问，第 {0} 次，共 {1} 次...", i + 1, simulateAccess);
                await Utility.HTTPGetStringAsync(userAgent, string.Format(PageUri, id), cookies);
                await Delay();
            }
        }

        private async Task Vote(int id)
        {
            lb_Status.Text = "正在刷票  ";
            
            string userAgent = Utility.RandomUserAgent();

            CookieContainer cookies = new CookieContainer();
            string captcha = null;

            while (!stopFlag)
            {
                if (captcha == null)
                {
                    captcha = await GetCaptcha(cookies, userAgent);
                }

                lb_Operation.Text = string.Format("正在提交，验证码为 {0}", captcha);
                string submitResult = await Utility.HTTPGetStringAsync(userAgent, string.Format(VoteUri, id, captcha), cookies);
                await Delay();

                if (submitResult.Contains("推荐成功"))
                {
                    voteCount++;
                    UpdateProgress();
                }
                else if(submitResult.Contains("验证码不符合"))
                {
                    captcha = null;
                }
                else
                {
                    throw new Exception("程序出现错误。可能是刷票途径被封。请联系作者。");
                }

                await SimulateAccess(cookies, userAgent, id);

                Uri sss;
                Uri.TryCreate("http://www.chinawenxing.com.cn/", UriKind.RelativeOrAbsolute, out sss);
                Cookie c = cookies.GetCookies(sss)["PHPSESSID"];
                cookies = new CookieContainer();
                cookies.Add(c);

                if (cb_MaxVote.Checked && voteCount >= num_MaxVote.Value)
                {
                    break;
                }
            }

            stopFlag = false;
            tb_Url.Enabled = true;
            btn_Start.Enabled = true;
            btn_Start.Text = "开始";
            btn_Start.Tag = null;
            lb_Status.Text = "刷票结束";
            lb_Operation.Text = "";
        }

        private async void btn_Start_Click(object sender, EventArgs e)
        {
            if (btn_Start.Tag == null)
            {

                Regex urlRegex = new Regex(@"(?:^|/?|&)nid=([^&]*)(?:&|$)");
                Match m = urlRegex.Match(tb_Url.Text.ToLower());
                if (m.Success)
                {
                    int id;
                    if (int.TryParse(m.Groups[1].Value, out id))
                    {
                        if (MessageBox.Show("此工具仅用于测试网站安全性，禁止用于其他用途。如果主办方检测到作弊行为，则比赛资格可能被取消。你是否愿意承担刷票行为所造成的全部后果？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            tb_Url.Enabled = false;
                            btn_Start.Text = "停止";
                            btn_Start.Tag = new object();
                            await Vote(id);
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                MessageBox.Show("请输入正确的网址！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                stopFlag = true;
                btn_Start.Enabled = false;
                btn_Start.Text = "正在停止";
            }
        }

        private void cb_MaxVote_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_MaxVote.Checked)
            {
                num_MaxVote.Enabled = true;
                pgb_Progress.Visible = true;
            }
            else
            {
                num_MaxVote.Enabled = false;
                pgb_Progress.Visible = false;
            }
        }

        private void cb_Delay_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_Delay.Checked)
            {
                num_Delay.Enabled = true;
            }
            else
            {
                num_Delay.Enabled = false;
            }
        }

        private void num_MaxVote_ValueChanged(object sender, EventArgs e)
        {
            if (num_MaxVote.Value < voteCount)
            {
                voteCount = 0;
            }
            UpdateProgress();
        }

        private void tl_ResetCounter_Click(object sender, EventArgs e)
        {
            voteCount = 0;
            UpdateProgress();
        }
    }
}
