/*
 * A simple SMTP verification system.  Not advised for a production system.
 * 
 * Thanks to: https://blog.elmah.io/how-to-send-emails-from-csharp-net-the-definitive-tutorial/
 *
 * If you use gmail, then you'll need to go into the GMail account security and turn on 
 * less secure application access.  In my experience, Yahoo tends to simply blacklists 
 * an IP that sends too many automated messages.
 * 
 * You can modify the code to directly access the SMTP server of the recipient, however, that's a
 * great way to get your IP blacklisted.  You can also host your own SMTP server, but again, that
 * is usually a royal pain and you run the risk of getting hacked and blacklisted unless you stay
 * up to date with security patches and best practices.
 *
 * Note: The built-in SMTP mail system is now considered obsolete by Microsoft  
 * and they recommend using MailKit https://github.com/jstedfast/MailKit as
 * the best way to handle mail.  It's much more capable and modern, so for
 * a production system, it's advised that you avoid the built in SMTP library.
 *
 * Additional MailKit info can be found at:
 * https://edi.wang/post/2019/4/14/send-email-in-net-core-via-mailkit
 * https://social.technet.microsoft.com/wiki/contents/articles/37534.asp-net-core-1-1-send-email-with-mailkit-in-visual-studio-2017.aspx
 * https://www.ryadel.com/en/asp-net-core-send-email-messages-smtp-mailkit/
 * https://dotnetcoretutorials.com/2017/11/02/using-mailkit-send-receive-email-asp-net-core/
 * 
 * 
 * The SendSMTP.xml file should look something like:
 * 
 *  <SendSMTP>
 *   <SMTPProperties>
 *     <Account>XXXX@gmail.com</Account>
 *     <ReplyTo>XXXX@gmail.com</ReplyTo>
 *     <Password>XXXXXXXX</Password>
 *     <Gateway>smtp.gmail.com</Gateway>
 *     <Port>587</Port>
 *     <EnableSSL>Y</EnableSSL>
 *     <UseHTML>Y</UseHTML>
 *     <UseAttachments>N</UseAttachments>
 *     <Subject>Welcome to Chat Services</Subject>
 *     <Message>Your verification code is <h2>{0}</h2></Message>
 *   </SMTPProperties>
 * </SendSMTP>
 */

using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Reflection;
using System.Xml;

namespace WPFChatServer
{
    class SendSMTP
    {
        public int ErrorCode = 0;
        public string ErrorMessage = string.Empty;

        private string message = "Your verification code is {0}.";
        private string subject = "Chat Server Verification";
        private bool HTMLMsg = false;

        private string smtpGateway = "##fake-address.com";
        private string hostAccount = "noreply@##fake-address.com";
        private string replyAccount = "noreply@##fake-address.com";
        private string hostPassword = "password";
        private int hostPort = 587;
        private bool hostSSL = true;
        private bool allowAttachments = false;

        private SmtpClient smtpClient;
        private Attachment attachment;
        private MailMessage mailMessage;

        private readonly string LocalPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";

        public SendSMTP()
        {
            LoadConfig();

            // set up the SMTP client
            smtpClient = new SmtpClient(smtpGateway)
            {
                Port = hostPort,
                Credentials = new NetworkCredential(hostAccount, hostPassword),
                EnableSsl = hostSSL,
            };
        }

        public void LoadConfig()
        {
            string configFile = LocalPath + "SendSMTP.xml";
            int ivalue;
            string value;
            string setting;

            if (File.Exists(configFile))
            {
                XmlDocument xmldoc = new XmlDocument();
                XmlNodeList xmlnode;
                FileStream fs = new FileStream(configFile, FileMode.Open, FileAccess.Read);

                xmldoc.Load(fs);
                xmlnode = xmldoc.GetElementsByTagName("SMTPProperties");

                for (int i = 0; i <= xmlnode.Count - 1; i++)
                {
                    for (int j = 0; j < xmlnode[i].ChildNodes.Count; j++)
                    {
                        value = xmlnode[i].ChildNodes.Item(j).InnerText.Trim();
                        if (int.TryParse(value, out ivalue) == false) ivalue = 0;

                        setting = xmlnode[i].ChildNodes.Item(j).Name.ToUpper().Trim();

                        switch (setting)
                        {
                            case "SUBJECT":
                                // assume false unless first character of value contains a specific character
                                subject = value;
                                break;

                            case "MESSAGE":
                                message = value;
                                break;

                            case "GATEWAY":
                                smtpGateway = value;
                                break;

                            case "ACCOUNT":
                                hostAccount = value;
                                break;

                            case "REPLYTO":
                                replyAccount = value;
                                break;

                            case "PASSWORD":
                                hostPassword = value;
                                break;

                            case "PORT":
                                hostPort = ivalue;
                                break;

                            case "USEHTML":
                                HTMLMsg = "Y1T".Contains((value + "N").Substring(0, 1).ToUpper());
                                break;

                            case "USESSL":
                                hostSSL = "Y1T".Contains((value + "N").Substring(0, 1).ToUpper());
                                break;

                            case "USEATTACHMENTS":
                                allowAttachments= "Y1T".Contains((value + "N").Substring(0, 1).ToUpper());
                                break;

                            default:
                                break;
                        }
                    }
                }
            }
        }

        public bool AddAttachment(string UNCname)
        {
            bool Result = true;

            if (allowAttachments)
            {
                try
                {
                    string ext = Path.GetExtension(UNCname);

                    switch (ext)
                    {
                        case ".JPG":
                            attachment = new Attachment(UNCname, MediaTypeNames.Image.Jpeg);
                            break;

                        case ".GIF":
                            attachment = new Attachment(UNCname, MediaTypeNames.Image.Gif);
                            break;

                        case ".TIF":
                        case ".TIFF":
                            attachment = new Attachment(UNCname, MediaTypeNames.Image.Tiff);
                            break;

                        case ".PDF":
                            attachment = new Attachment(UNCname, MediaTypeNames.Application.Pdf);
                            break;

                        case ".ZIP":
                            attachment = new Attachment(UNCname, MediaTypeNames.Application.Zip);
                            break;

                        case ".HTM":
                        case ".HTML":
                            attachment = new Attachment(UNCname, MediaTypeNames.Text.Html);
                            break;

                        case ".TXT":
                            attachment = new Attachment(UNCname, MediaTypeNames.Text.Plain);
                            break;

                        case ".XML":
                            attachment = new Attachment(UNCname, MediaTypeNames.Text.Xml);
                            break;

                        default:
                            attachment = new Attachment(UNCname, MediaTypeNames.Application.Octet);
                            break;
                    }

                    mailMessage.Attachments.Add(attachment);
                }
                catch (Exception ex)
                {
                    ErrorCode = 900;
                    ErrorMessage = ex.Message;
                    Result = false;
                }
            }
            else
            {
                ErrorCode = 900;
                ErrorMessage = "Attachments are not allowed";
                Result = false;
            }

            return Result;
        }

        public bool SendEmail(string recipient)
        {
            return SendVerifyCode(recipient, "");
        }

        public bool SendVerifyCode(string recipient, string vcode)
        {
            bool Result = true;

            try
            {
                mailMessage = new MailMessage
                {
                    From = new MailAddress(replyAccount),
                    Subject = subject,
                    Body = (vcode.Length > 0 ? string.Format(message, vcode) : message),
                    IsBodyHtml = HTMLMsg,
                };

                mailMessage.To.Add(recipient);
                smtpClient.Send(mailMessage);
            }
            catch (Exception ex)
            {
                ErrorCode = 900;
                ErrorMessage = ex.Message;
                Result = false;
            }

            return Result;
        }
    }
}
