using System;
using System.Linq;
using System.Net;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Shipwreck.KokoroIO;
using System.Collections.Generic;

namespace KokoroIoGitHubNotificationBot
{
    public enum EventTypes
    {
        Unknown = 0, CommitComment, Create, Delete, Deployment, DeploymentStatus, Download, Follow, Fork, ForkApply, Gist, Gollum, Installation, InstallationRepositories, IssueComment, Issues, Label, MarketplacePurchase, Member, Membership, Milestone, Organization, OrgBlock, PageBuild, ProjectCard, ProjectColumn, Project, Public, PullRequest, PullRequestReview, PullRequestReviewComment, Push, Release, Repository, Status, Team, TeamAdd, Watch
    }
    public static class IncomingWebhook
    {
        [FunctionName("IncomingWebhook")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(WebHookType = "github")]HttpRequestMessage req, TraceWriter log)
        {
            // Get request body
            dynamic data = await req.Content.ReadAsAsync<object>();

            var channelId = req.GetQueryNameValuePairs().FirstOrDefault(kv => kv.Key == "channel").Value;

            var accessToken = ConfigurationManager.AppSettings.Get("AccessToken");

            if (string.IsNullOrEmpty(accessToken))
                return req.CreateResponse(HttpStatusCode.BadRequest, "Missing configration: AccessToken");

            if (string.IsNullOrEmpty(channelId))
                return req.CreateResponse(HttpStatusCode.BadRequest, "Missing parameter: channel");

            var eventType = GetEventType(req);
            var repositoryMeta = $"\\[[{ data.repository.full_name }]({ data.repository.html_url })\\]";
            var eventDescription = "Unknown event";
            var eventMessage = "No description";

            switch(eventType)
            {
                case EventTypes.IssueComment:
                    var action = data?.action;
                    eventDescription = $"New comment { action } by [{ data.comment.user.login }]({ data.comment.user.html_url }) on issue [#{ data.issue.number }: { data.issue.title }]({ data.comment.html_url })";
                    eventMessage = $"{ data.comment.body }";
                    break;
                default:
                    break;

            }
            var message = $@"
__{ repositoryMeta }__
__{ eventType }__
> { eventMessage }";
            log.Info($"Channel: { channelId }");
            log.Info($"Message: { message }");

            using (var bot = new BotClient() { AccessToken = accessToken })
            {
                await bot.PostMessageAsync(channelId, message);
            }

            return req.CreateResponse(HttpStatusCode.OK, message);
        }

        private static EventTypes GetEventType(HttpRequestMessage req)
        {
            if(!req.Headers.TryGetValues("X-GitHub-Event", out IEnumerable<string> values))
                return EventTypes.Unknown;

            var eventName = values.FirstOrDefault()?.Replace("_", "");

            EventTypes.TryParse(eventName, true, out EventTypes eventType);
            return eventType;
        }
    }
}