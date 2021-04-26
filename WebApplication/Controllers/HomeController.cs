using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTokFs.RequestDomain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WebApplication.Models;

namespace WebApplication.Controllers {
    public class HomeController : Controller {
        private readonly ExampleDbContext _context;

        public HomeController(ExampleDbContext context) {
            _context = context;
        }

        public async Task<IActionResult> Index() {
            var list = await _context.VonageVideoAPIProjectCredentials.ToListAsync();
            return View(list);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProjectPost(int key, string secret) {
            var credentials = new OpenTokFs.Credentials.AccountCredentials(key, secret);
            var project = await OpenTokFs.Api.Project.CreateAsync(credentials, ProjectName.NoProjectName);
            _context.VonageVideoAPIProjectCredentials.Add(new VonageVideoAPIProjectCredential {
                ApiKey = project.Id,
                ApiSecret = project.Secret
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProjectPost(int key, string secret) {
            _context.VonageVideoAPIProjectCredentials.Add(new VonageVideoAPIProjectCredential {
                ApiKey = key,
                ApiSecret = secret
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgetProject(int id) {
            var sessions = await _context.VonageVideoAPISessions.Where(x => x.Project.ApiKey == id).ToListAsync();
            _context.VonageVideoAPISessions.RemoveRange(sessions);

            var project = await _context.VonageVideoAPIProjectCredentials.Where(x => x.ApiKey == id).SingleAsync();
            _context.VonageVideoAPIProjectCredentials.Remove(project);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ListSessions(int projectId) {
            var list = await _context.VonageVideoAPISessions.Where(s => s.Project.ApiKey == projectId).ToListAsync();
            return View(list);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSessionPost(int projectId) {
            var project = await _context.VonageVideoAPIProjectCredentials.Where(x => x.ApiKey == projectId).SingleAsync();
            var session = await OpenTokFs.Api.Session.CreateAsync(project, NewSession.Default);
            _context.VonageVideoAPISessions.Add(new VonageVideoAPISession {
                Project = project,
                SessionId = session.Session_id
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ListSessions), new { projectId = projectId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgetSession(string id) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == id).SingleAsync();
            int projectId = session.Project.ApiKey;

            _context.VonageVideoAPISessions.Remove(session);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ListSessions), new { projectId = projectId });
        }

        [HttpGet]
        public async Task<IActionResult> Session(string id) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == id).SingleAsync();
            ViewBag.Token = OpenTokFs.OpenTokSessionTokens.GenerateToken(session.Project, new OpenTokFs.OpenTokSessionTokenParameters(session.SessionId) {
                ExpireTime = DateTimeOffset.UtcNow.AddMinutes(1)
            });
            return View(session);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> StartArchive(
            string sessionId,
            bool hasAudio,
            bool hasVideo,
            string layout,
            string layoutCss,
            string name,
            bool composed,
            string resolution
        ) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            await OpenTokFs.Api.Archive.StartAsync(session.Project, new ArchiveStartRequest(
                sessionId,
                hasAudio: hasAudio,
                hasVideo: hasVideo,
                name: string.IsNullOrWhiteSpace(name)
                    ? ArchiveName.NoArchiveName
                    : ArchiveName.NewCustomArchiveName(name),
                outputType: !composed
                    ? ArchiveOutputType.IndividualArchive
                    : ArchiveOutputType.NewComposedArchive(
                        resolution == "1280x720"
                            ? Resolution.HighDefinition
                            : Resolution.StandardDefinition,
                        !string.IsNullOrWhiteSpace(layoutCss) ? Layout.NewCustomCss(layoutCss)
                            : layout == "Best Fit" ? Layout.NewStandard(StandardLayout.BestFit)
                            : layout == "Picture-in-Picture" ? Layout.NewStandard(StandardLayout.Pip)
                            : layout == "Vertical Presentation" ? Layout.NewStandard(StandardLayout.VerticalPresentation)
                            : layout == "Horizontal Presentation" ? Layout.NewStandard(StandardLayout.HorizontalPresentation)
                            : Layout.NewStandard(StandardLayout.BestFit))));
            return NoContent();
        }

        private static readonly ListParameters SingleItem = new ListParameters(
            new PageBoundaries(0, 1),
            ListLimit.NewStopAtItemCount(1));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> StopArchive(string sessionId) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            var archives = await OpenTokFs.Api.Archive.ListAllAsync(
                session.Project,
                SingleItem,
                SessionIdFilter.NewSingleSessionId(sessionId));
            var a = archives.SingleOrDefault();
            if (a != null) {
                if (a.Status == "started" || a.Status == "paused") {
                    var stopped = await OpenTokFs.Api.Archive.StopAsync(session.Project, a.Id);

                    for (int i = 0; i < 5 && stopped.Status != "uploaded" && stopped.Status != "available"; i++) {
                        await Task.Delay(3000);
                        stopped = await OpenTokFs.Api.Archive.GetAsync(session.Project, a.Id);
                    }

                    if (stopped.Url == null)
                        return Content("Archive stopped, but URL not available");
                    else if (stopped.Url.Contains("archive.mp4", StringComparison.InvariantCultureIgnoreCase))
                        return Redirect(stopped.Url);
                    else
                        return Content(stopped.Url, "text/plain");
                }
            }
            return NoContent();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> StartBroadcast(
            string sessionId,
            string layout,
            string layoutCss,
            int maxDuration,
            bool hls,
            string rtmpServerUrl,
            string rtmpStreamName,
            string resolution
        ) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            var broadcasts = await OpenTokFs.Api.Broadcast.ListAllAsync(
                session.Project,
                SingleItem,
                SessionIdFilter.NewSingleSessionId(sessionId));
            var broadcast = broadcasts.SingleOrDefault();
            if (broadcast == null) {
                var req = new BroadcastStartRequest(
                    sessionId: sessionId,
                    layout: !string.IsNullOrWhiteSpace(layoutCss) ? Layout.NewCustomCss(layoutCss)
                            : layout == "Best Fit" ? Layout.NewStandard(StandardLayout.BestFit)
                            : layout == "Picture-in-Picture" ? Layout.NewStandard(StandardLayout.Pip)
                            : layout == "Vertical Presentation" ? Layout.NewStandard(StandardLayout.VerticalPresentation)
                            : layout == "Horizontal Presentation" ? Layout.NewStandard(StandardLayout.HorizontalPresentation)
                            : Layout.NewStandard(StandardLayout.BestFit),
                    maxDuration: TimeSpan.FromSeconds(maxDuration),
                    outputs: hls
                        ? BroadcastTargets.HlsOnly
                        : BroadcastTargets.RtmpOnly(rtmpServerUrl, rtmpStreamName),
                    resolution: resolution == "1280x720" ? Resolution.HighDefinition : Resolution.StandardDefinition);
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(req);
                broadcast = await OpenTokFs.Api.Broadcast.StartAsync(
                    session.Project,
                    req);
            }
            return View("Player", broadcast);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> StopBroadcast(string sessionId) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            var broadcasts = await OpenTokFs.Api.Broadcast.ListAllAsync(
                session.Project,
                SingleItem,
                SessionIdFilter.NewSingleSessionId(sessionId));
            var b = broadcasts.SingleOrDefault();
            if (b != null){
                await OpenTokFs.Api.Broadcast.StopAsync(session.Project, b.Id);
            }
            return View("Player", null);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeLayout(string sessionId, string layout, string screenSharingLayout, string layoutCss) {
            var l = !string.IsNullOrWhiteSpace(layoutCss) ? Layout.NewCustomCss(layoutCss)
                    : screenSharingLayout == "Picture-in-Picture" ? Layout.NewBestFitOr(ScreenshareType.NewScreenshareType(StandardLayout.Pip))
                    : screenSharingLayout == "Vertical Presentation" ? Layout.NewBestFitOr(ScreenshareType.NewScreenshareType(StandardLayout.VerticalPresentation))
                    : screenSharingLayout == "Horizontal Presentation" ? Layout.NewBestFitOr(ScreenshareType.NewScreenshareType(StandardLayout.HorizontalPresentation))
                    : layout == "Best Fit" ? Layout.NewStandard(StandardLayout.BestFit)
                    : layout == "Picture-in-Picture" ? Layout.NewStandard(StandardLayout.Pip)
                    : layout == "Vertical Presentation" ? Layout.NewStandard(StandardLayout.VerticalPresentation)
                    : layout == "Horizontal Presentation" ? Layout.NewStandard(StandardLayout.HorizontalPresentation)
                    : Layout.NewStandard(StandardLayout.BestFit);
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            var broadcasts = await OpenTokFs.Api.Broadcast.ListAllAsync(
                session.Project,
                SingleItem,
                SessionIdFilter.NewSingleSessionId(sessionId));
            foreach (var b in broadcasts) {
                await OpenTokFs.Api.Broadcast.SetLayoutAsync(session.Project, b.Id, l);
            }
            var archives = await OpenTokFs.Api.Archive.ListAllAsync(
               session.Project,
               SingleItem,
               SessionIdFilter.NewSingleSessionId(sessionId));
            foreach (var a in archives) {
                if (a.Status == "started" || a.Status == "paused") {
                    await OpenTokFs.Api.Archive.SetLayoutAsync(session.Project, a.Id, l);
                }
            }
            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> MakeBig(string sessionId, string streamId) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            await OpenTokFs.Api.Session.SetLayoutClassesAsync(
                session.Project,
                session.SessionId,
                new LayoutClassChangeRequest(new [] {
                    new LayoutClassChangeItem(streamId, new[] {"full", "focus"})
                }));
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> ListArchives(string sessionId, int max) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            var archives = await OpenTokFs.Api.Archive.ListAllAsync(
                session.Project,
                new ListParameters(
                    new PageBoundaries(0, Math.Min(1000, max)),
                    ListLimit.NewStopAtItemCount(max)),
                SessionIdFilter.NewSingleSessionId(sessionId));
            return Ok(archives);
        }
    }
}
