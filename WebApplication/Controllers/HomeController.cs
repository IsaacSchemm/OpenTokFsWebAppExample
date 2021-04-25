﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WebApplication.Models;

using R = OpenTokFs.RequestDomain;

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
            var session = await OpenTokFs.Api.Session.CreateAsync(project, new OpenTokFs.Api.Session.CreationParameters());
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
            await OpenTokFs.Api.Archive.StartAsync(session.Project, new R.ArchiveStartRequest(
                sessionId,
                new R.ArchiveStartParameters(
                    hasAudio: hasAudio,
                    hasVideo: hasVideo,
                    name: string.IsNullOrWhiteSpace(name)
                        ? R.ArchiveName.NoArchiveName
                        : R.ArchiveName.NewCustomArchiveName(name),
                    outputType: !composed
                        ? R.OutputType.Individual
                        : R.OutputType.NewComposed(
                            resolution == "1280x720"
                                ? R.Resolution.HD
                                : R.Resolution.SD,
                            !string.IsNullOrWhiteSpace(layoutCss)
                                ? R.Layout.NewCustomCss(layoutCss)
                                : R.Layout.NewBuiltIn(layout == "Best Fit" ? R.LayoutType.BestFit
                                    : layout == "Picture-in-Picture" ? R.LayoutType.PIP
                                    : layout == "Vertical Presentation" ? R.LayoutType.VerticalPresentation
                                    : layout == "Horizontal Presentation" ? R.LayoutType.HorizontalPresentation
                                    : R.LayoutType.BestFit)))));
            return NoContent();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> StopArchive(string sessionId) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            var archives = await OpenTokFs.Api.Archive.ListAllAfterAsync(
                session.Project,
                DateTimeOffset.UtcNow.AddDays(-1),
                OpenTokFs.OpenTokSessionId.NewId(sessionId));
            foreach (var a in archives) {
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
                int.MaxValue,
                OpenTokFs.OpenTokSessionId.NewId(sessionId));
            var broadcast = broadcasts.SingleOrDefault();
            if (broadcast == null) {
                var req = new OpenTokFs.RequestTypes.OpenTokBroadcastStartRequest(sessionId) {
                    Layout = !string.IsNullOrEmpty(layoutCss) ? OpenTokFs.RequestTypes.OpenTokVideoLayout.Custom(layoutCss)
                            : layout == "Best Fit" ? OpenTokFs.RequestTypes.OpenTokVideoLayout.BestFit
                            : layout == "Picture-in-Picture" ? OpenTokFs.RequestTypes.OpenTokVideoLayout.Pip
                            : layout == "Vertical Presentation" ? OpenTokFs.RequestTypes.OpenTokVideoLayout.VerticalPresentation
                            : layout == "Horizontal Presentation" ? OpenTokFs.RequestTypes.OpenTokVideoLayout.HorizontalPresentation
                            : OpenTokFs.RequestTypes.OpenTokVideoLayout.BestFit,
                    Duration = TimeSpan.FromSeconds(maxDuration),
                    Hls = hls,
                    Resolution = resolution,
                    Rtmp = new[] {
                            new OpenTokFs.RequestTypes.OpenTokRtmpDestination {
                                ServerUrl = rtmpServerUrl,
                                StreamName = rtmpStreamName
                            }
                        }.Where(x => !string.IsNullOrEmpty(x.ServerUrl) && !string.IsNullOrEmpty(x.StreamName))
                };
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
                int.MaxValue,
                OpenTokFs.OpenTokSessionId.NewId(sessionId));
            foreach (var b in broadcasts) {
                await OpenTokFs.Api.Broadcast.StopAsync(session.Project, b.Id);
            }
            return View("Player", null);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeLayout(string sessionId, string layout, string layoutCss) {
            var l = !string.IsNullOrEmpty(layoutCss) ? OpenTokFs.RequestTypes.OpenTokVideoLayout.Custom(layoutCss)
                    : layout == "Best Fit" ? OpenTokFs.RequestTypes.OpenTokVideoLayout.BestFit
                    : layout == "Picture-in-Picture" ? OpenTokFs.RequestTypes.OpenTokVideoLayout.Pip
                    : layout == "Vertical Presentation" ? OpenTokFs.RequestTypes.OpenTokVideoLayout.VerticalPresentation
                    : layout == "Horizontal Presentation" ? OpenTokFs.RequestTypes.OpenTokVideoLayout.HorizontalPresentation
                    : OpenTokFs.RequestTypes.OpenTokVideoLayout.BestFit;
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            var broadcasts = await OpenTokFs.Api.Broadcast.ListAllAsync(
                session.Project,
                int.MaxValue,
                OpenTokFs.OpenTokSessionId.NewId(sessionId));
            foreach (var b in broadcasts) {
                await OpenTokFs.Api.Broadcast.SetLayoutAsync(session.Project, b.Id, l);
            }
            var archives = await OpenTokFs.Api.Archive.ListAllAfterAsync(
               session.Project,
               DateTimeOffset.UtcNow.AddDays(-1),
               OpenTokFs.OpenTokSessionId.NewId(sessionId));
            foreach (var a in archives) {
                if (a.Status == "started" || a.Status == "paused") {
                    await OpenTokFs.Api.Archive.SetLayoutAsync(session.Project, a.Id, l);
                }
            }
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> ListArchives(string sessionId, int max) {
            var session = await _context.VonageVideoAPISessions.Include(x => x.Project).Where(x => x.SessionId == sessionId).SingleAsync();
            var archives = await OpenTokFs.Api.Archive.ListAllAsync(
                session.Project,
                max,
                OpenTokFs.OpenTokSessionId.NewId(sessionId));
            return Ok(archives);
        }
    }
}
