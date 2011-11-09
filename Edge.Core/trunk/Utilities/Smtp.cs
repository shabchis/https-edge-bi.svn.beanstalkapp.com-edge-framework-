using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.Collections;
using System.Net;
using System.Configuration;

namespace Edge.Core.Utilities
{
	public class Smtp
	{
		private static string ToAddress { set; get; }
		private static string FromAddress { set; get; }

		public static void SetFromTo(string from, string to)
		{
			Smtp.FromAddress = from;
			Smtp.ToAddress = to;
		}

		public static void Send(string subject,string body,bool highPriority = false, bool IsBodyHtml = false, string attachmentPath = null)
		{
			if (string.IsNullOrEmpty(ToAddress) || string.IsNullOrEmpty(FromAddress))
				throw new ArgumentNullException("Address cannot be empty");
			
			try
			{
				SmtpClient smtp = Smtp.GetSmtpConnection();
				MailAddress from = new MailAddress(FromAddress);
				MailAddress to = new MailAddress(ToAddress);
				MailMessage msg = new MailMessage(from, to);
				msg.Subject = subject;
				if (highPriority)
					msg.Priority = MailPriority.High;
				if (!String.IsNullOrEmpty(body)) msg.Body = body;
				if (IsBodyHtml) msg.IsBodyHtml = true;
				else msg.IsBodyHtml = false;
				
				if (!String.IsNullOrEmpty(attachmentPath))
				{
					msg.Attachments.Add(new Attachment(attachmentPath));
				}
				smtp.Send(msg);
			}
			catch (Exception e)
			{
				throw new Exception("Cannot send Email" + e.Message);
			}
		}
		private static SmtpClient GetSmtpConnection()
		{
			try
			{
				
				IDictionary smtpCon = GetConfigurationSection("SmtpConnection");
				SmtpClient smtp = new SmtpClient();
				smtp.Host = smtpCon["server"].ToString();
				smtp.Port = Convert.ToInt32(smtpCon["port"].ToString());
				smtp.Credentials = new NetworkCredential(smtpCon["user"].ToString(), Core.Utilities.Encryptor.Dec(smtpCon["pass"].ToString()));
				//smtp.UseDefaultCredentials = Boolean.Parse(smtpCon["UseDefaultCredentials"].ToString());
				//smtp.EnableSsl = Boolean.Parse(smtpCon["EnableSsl"].ToString());

				return smtp;
			}
			catch (Exception ex)
			{
				throw new Exception("SMTP Configuration Error" + ex.Message);
			}
		}

		private static IDictionary GetConfigurationSection(string sectionName)
		{
			IDictionary val = new Dictionary<String, String>();
			try
			{

				val = (IDictionary)(ConfigurationManager.GetSection(sectionName));
				return val;
			}
			catch (Exception e)
			{
				throw new Exception("Configuration Error", e);
			}
			//if (val == null) throw new Exception(string.Format("Configuration Error: {0} cannot be null",sectionName) ;

		}
	}
}
