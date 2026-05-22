using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

class Program
{
    static void Main()
    {
        try
        {
            using var client = new TcpClient("192.54.136.152", 8061);
            using var sslStream = new SslStream(client.GetStream(), false, (sender, certificate, chain, sslPolicyErrors) => true);
            sslStream.AuthenticateAsClient("192.54.136.152", null, SslProtocols.None, false);
            Console.WriteLine("SSL Handshake Succeeded");

            string logon = "8=FIX.4.4\x01" + "9=107\x01" + "35=A\x01" + "34=1\x01" + "49=FINTECHEE\x01" + "52=" + DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff") + "\x01" + "56=SPOTEX\x01" + "98=0\x01" + "108=30\x01" + "141=Y\x01" + "553=FINTECHEE\x01" + "554=fintechee123\x01" + "10=096\x01";
            // Note: Checksum 096 is just a placeholder, but some servers don't care about checksum on logon or will send a logout with the reason.
            
            byte[] data = Encoding.ASCII.GetBytes(logon);
            sslStream.Write(data);
            sslStream.Flush();

            byte[] buffer = new byte[4096];
            int bytesRead = sslStream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                Console.WriteLine("Received: " + Encoding.ASCII.GetString(buffer, 0, bytesRead).Replace('\x01', '|'));
            }
            else
            {
                Console.WriteLine("No response");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            if (ex.InnerException != null) Console.WriteLine("Inner: " + ex.InnerException.Message);
        }
    }
}
