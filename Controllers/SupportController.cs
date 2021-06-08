using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using IT_Stat.Models;

namespace IT_Stat.Controllers
{
    public class SupportController : Controller
    {
        readonly IWebHostEnvironment _hostEnvironment;
        readonly ServiceDesk _db;
        public SupportController(IWebHostEnvironment hostEnvironment, ServiceDesk context)
        {
            _hostEnvironment = hostEnvironment;
            _db = context;
        }
        public IActionResult Reaction(DateTime start, DateTime end)
        {
            Program.logger.Info("Обращение к отчету по времени реакции");
            DateTime def = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ViewBag.Start = (start == DateTime.MinValue ? def.AddMonths(-1).AddDays(25) : start).ToString("yyyy.MM.dd");
            ViewBag.End = (end == DateTime.MinValue ? def.AddDays(24) : end).ToString("yyyy.MM.dd");
            return View(Support.Reaction(ViewBag.Start, ViewBag.End, _db));
        }
        public IActionResult Fact(DateTime start, DateTime end)
        {
            Program.logger.Info("Обращение к отчету по выполненным заявкам");
            DateTime def = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ViewBag.Start = (start == DateTime.MinValue ? def.AddMonths(-1).AddDays(25) : start).ToString("yyyy.MM.dd");
            ViewBag.End = (end == DateTime.MinValue ? def.AddDays(24) : end).ToString("yyyy.MM.dd");
            return View(Support.Fact(ViewBag.Start, ViewBag.End, _db));
        }
        public IActionResult Complete(DateTime start, DateTime end)
        {
            Program.logger.Info("Обращение к отчету по выполненным заявкам");
            DateTime def = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ViewBag.Start = (start == DateTime.MinValue ? def.AddMonths(-1).AddDays(25) : start).ToString("yyyy.MM.dd");
            ViewBag.End = (end == DateTime.MinValue ? def.AddDays(24) : end).ToString("yyyy.MM.dd");
            return View(Support.Complete(ViewBag.Start, ViewBag.End, _db));
        }
    }
}