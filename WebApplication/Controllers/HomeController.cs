using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    }
}
