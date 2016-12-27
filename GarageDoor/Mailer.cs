using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;

namespace GarageDoor
{
    class Mailer
    {
        var fromAddress = new MailAddress("from@gmail.com", "From Name");
        var toAddress = new MailAddress("to@example.com", "To Name");
        const string fromPassword = "fromPassword";
        const string subject = "Subject";
        const string body = "Body";

        var smtp = new SmtpClient
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
        };

    }
}
