using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SVNWebexNotifier.Helpers;
using SVNWebexNotifier.Models;

namespace SVNWebexNotifier
{
    public class SlackNotifier
    {
        public event EventHandler OnFinished = null;

        public async Task<bool> PostNotificationAsync(Notification notification)
        {
            var isSuccess = false;

            try
            {
                if (string.IsNullOrEmpty(ConfigurationHelper.SlackWebhookURL))
                    Logger.Shared.WriteError("Missing Slack Webhook URL");
                else if(ConfigurationHelper.SlackWebhookURL == "https://hooks.slack.com/services/foo/bar/baz")
                    Logger.Shared.WriteError("Found default Slack Webhook URL in config file. Ensure you've replaced it with your own.");
                else if (string.IsNullOrEmpty(notification.RepositoryPath))
                    Logger.Shared.WriteError("Missing repo path");
                else if (string.IsNullOrEmpty(notification.Revision))
                    Logger.Shared.WriteError("Missing revision number");
                else
                {
                    // Supporting protocol TLS 1.0 / 1.1 / 1.2
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                    // Post to Slack
                    using (var client = new HttpClient())
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, ConfigurationHelper.SlackWebhookURL);
                        string string_payload = BuildPayload(notification);
                        if (String.IsNullOrEmpty(string_payload))
                        {
                            return isSuccess;
                        }

                        var payload = string_payload;
                        //Logger.Shared.WriteDebug(payload);
                        request.Content = new StringContent(payload, null /*Encoding.UTF8*/, "application/json");
                        using (var response = await client.SendAsync(request))
                        using (var content = response.Content)
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                var result = await content.ReadAsStringAsync();
                                Logger.Shared.WriteError("Failed to send notification: " + response.StatusCode + " => " + result);
                                if (result == "Payload was not valid JSON")
                                    Logger.Shared.WriteError("payload = " + payload);
                            }
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Logger.Shared.WriteError(e);
            }

            if (OnFinished != null)
                OnFinished(this, null);

            return isSuccess;
        }

        private string BuildPayload(Notification notification)
        {
            // Get some more data about this commit via "svnlook"
            notification.CommitMessage = CommandLineHelper.ExecuteProcess(ConfigurationHelper.SVNLookProcessPath, string.Format("log -r {0} {1}", notification.Revision, notification.RepositoryPath));
            notification.CommitAuthor = CommandLineHelper.ExecuteProcess(ConfigurationHelper.SVNLookProcessPath, string.Format("author -r {0} {1}", notification.Revision, notification.RepositoryPath));
            notification.CommitChanged = CommandLineHelper.ExecuteProcess(ConfigurationHelper.SVNLookProcessPath, string.Format("changed -r {0} {1}", notification.Revision, notification.RepositoryPath));

            if (!notification.CommitChanged.Contains(notification.RepositoryName))
            {
                return null;
            }

                // Ensure valid formatting of message
            if (notification.CommitMessage.Contains("\""))
                notification.CommitMessage = notification.CommitMessage.Replace("\"", "\\\"");
            if (notification.CommitMessage.Contains("\r"))
                notification.CommitMessage = notification.CommitMessage.Replace("\r", "");
            if (notification.CommitMessage.Contains("\n"))
                notification.CommitMessage = notification.CommitMessage.Replace("\n", "<br>");
            if (notification.CommitAuthor.Contains("\""))
                notification.CommitAuthor = notification.CommitAuthor.Replace("\"", "\\\"");
            if (notification.CommitChanged.Contains("\""))
                notification.CommitChanged = notification.CommitChanged.Replace("\"", "\\\"");
            if (notification.CommitChanged.Contains("\r"))
                notification.CommitChanged = notification.CommitChanged.Replace("\r", "");
            if (notification.CommitChanged.Contains("\n"))
                notification.CommitChanged = notification.CommitChanged.Replace("\n", "<br>");

            // Trim off unnecessary trailing CRLFs
            notification.CommitMessage = notification.CommitMessage.TrimEnd(new char[] { '\r', '\n' });
            notification.CommitAuthor = notification.CommitAuthor.TrimEnd(new char[] { '\r', '\n' });

            // Use advanced message formatting for incoming webhooks
            var payloadBody = new StringBuilder();
            payloadBody.Append("{");    // begin payload

            if (!string.IsNullOrEmpty(notification.RepositoryURL))
            {
                if (notification.RepositoryURL.Contains("/svn/"))
                    notification.RepositoryURL = notification.RepositoryURL.Replace("/svn/", "/!/#");
                    notification.RepositoryURL += "/commit/r" + notification.Revision;
            }

            payloadBody.Append(string.Format(" \"markdown\" : \"■-----<br>**From Tomey VisualSVN Server**  <br>*<{0}> New commit by {1}*<br>**r{2}:** {3}<br>{4}<br>■-----\" ",
                notification.RepositoryName, notification.CommitAuthor, 
                notification.Revision, notification.CommitMessage, notification.CommitChanged));

            payloadBody.Append("}"); // end payload

            return payloadBody.ToString();
        }
    }
}
