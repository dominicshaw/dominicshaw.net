using System;
using System.Net.Mail;
using System.Web.UI;

namespace dominicshaw.net
{
    public partial class SendEmail : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var msg = new MailMessage();

            msg.To.Add(new MailAddress("dominicshaw@gmail.com", "Dominic Shaw"));
            msg.Subject = "Website Enquiry: " + Request.Form["subj"];
            msg.IsBodyHtml = true;
            msg.Body = string.Format("<p>From: {0} ({1})</p>", Request.Form["name"], Request.Form["email"]);
            msg.Body += string.Format("<p>{0}</p>", Request.Form["msg"]);

            msg.Body = msg.Body.Replace("\n", "<br />");

            using (var client = new SmtpClient())
            {
                client.Send(msg);
            }
        }
    }
}