﻿using MyShuttle.Client.Core.DocumentResponse;
using MyShuttle.Diagnostics.Service.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Http;
using System.Web.Http.Results;
using MyShuttle.Client.Services;
using MyShuttle.Client.SharedLibrary.Cache;
using Newtonsoft.Json;
using System.Diagnostics;
using MyShuttle.Client.Web.Models;

public class HomeController : Controller
{
    private static string m_cachedResponse = null;
    private static DriverCache m_driverCache = new DriverCache();
    private static List<Driver> m_cachedDrivers = null;

    public ActionResult Index()
    {
        return View();
    }

    public ActionResult About()
    {
        ViewBag.Message = "Your application description page.";

        return View();
    }

    public ActionResult Contact()
    {
        ViewBag.Message = "Your contact page.";

        return View();
    }

    public JsonResult Vehicles()
    {
        Debug.WriteLine("Loading Vehicles...");
        var vehicles = VehiclesModel.Vehicles;

        var json = this.Json(vehicles, JsonRequestBehavior.AllowGet);
        json.MaxJsonLength = int.MaxValue;
        SaveJpegImages(vehicles);
        return json;
    }


    [System.Web.Mvc.HttpPost]
    public JsonResult Driver(string driverLookup)
    {
        Console.WriteLine("Loading Drivers...");
        var driver = GetIndividualDriverCached(driverLookup);
        Driver clientDriver = null;

        Console.WriteLine("Getting driver ratings...");
        try
        {
            using (var db = new RatingsContext())
            {
                var results = from driverRating in db.DriverRatings
                              where driverRating.DriverId == driver.DriverId
                              select driverRating;

                //double ratingAvg = results.First().RatingAvg;
                //string ratingText = String.Format("{0:F1} out of 5", ratingAvg);

                string ratingText = results.Count() > 0
                    ? String.Format("{0:F1} out of 5", results.First().RatingAvg)
                    : "Unrated";

                clientDriver = new Driver()
                {
                    Name = driver.Name,
                    PictureUrl = driver.PictureUrl,
                    RatingText = ratingText
                };
            }
        }
        catch (Exception)
        {
            // Fail gracefully in case the database connection goes down
        }
        
        Debug.WriteLine("Returning drivers...");
        var json = Json(clientDriver, JsonRequestBehavior.AllowGet);
        return json;
    }

    private Driver GetDriverFromDriverList(List<Driver> drivers, DriverLookupRequest driver)
    {
        Console.WriteLine("Getting Driver from Driver's List");
        Driver selectedDriver;
        try
        {
            selectedDriver = 
                drivers.Where(d => driver.id.Equals(d.DriverId)).FirstOrDefault();
        }
        catch (Exception)
        {
            // In case of failure return a placeholder driver record
            selectedDriver = new Driver()
            {
                DriverId = 0,
                Name = "Unknown",
                Picture = AppSettings.PlaceHolderPicture
            };
        }

        return selectedDriver;
    }

    private Driver GetIndividualDriver(string driverQuery)
    {

        var drivers = GetDriverList();
        var driverLookup = JsonConvert.DeserializeObject<DriverLookupRequest>(driverQuery);
        var driver = GetDriverFromDriverList(drivers, driverLookup);
        SaveJpegImage(driver);
        return driver;
    }

    private Driver GetIndividualDriverCached(string driverQuery)
    {

        var driver = m_driverCache.GetDriverFromCache(driverQuery);

        if (driver == null)
        {
            var drivers = GetDriverList(cacheResponse: true);
            var driverLookup = JsonConvert.DeserializeObject<DriverLookupRequest>(driverQuery);
            driver = GetDriverFromDriverList(drivers, driverLookup);
            m_driverCache.AddDriverToCache(driverQuery, driver);
            SaveJpegImage(driver);
        }
        return driver;
    }

    private List<Driver> GetDriverList(bool cacheResponse = false)
    {
        Console.WriteLine("Getting Driver's List from Backend");

        // Use the cached response if one exist
        string driversStr = m_cachedResponse;
        if (driversStr == null)
        {
            var request = new BaseRequest(AppSettings.DriversWebApiUrl, string.Empty);
            driversStr = request.GetString(AppSettings.DriversWebApiUrl);

            // cache this response if the caller wants us to
            if (cacheResponse)
            {
                m_cachedResponse = driversStr;
                m_cachedDrivers = DeserializeDriversJSON(driversStr);
            }
        }

        //return m_cachedDrivers;
        var drivers = DeserializeDriversJSON(driversStr);
        return drivers;
    }

    private List<Driver> DeserializeDriversJSON(string driversStr)
    {
        return JsonConvert.DeserializeObject<List<Driver>>(driversStr);
    }

    private void SaveJpegImage(Driver driver)
    {
        if (driver.Picture != null)
        {
            var pictureBytes = driver.Picture;
            driver.PictureUrl = "/Content/" + driver.Name + ".jpg";
            string path = System.Web.HttpContext.Current.Server.MapPath("~" + driver.PictureUrl);
            System.IO.File.WriteAllBytes(path, driver.Picture);
            driver.Picture = new byte[10 * 1024 * 1024];
        }
    }

    private void SaveJpegImages(IEnumerable<Vehicle> vehicles)
    {
        foreach (var vehicle in vehicles)
        {
            var pictureBytes = vehicle.Picture;
            vehicle.PictureUrl = "/Content/" + vehicle.Make + vehicle.Model + ".jpg";
            string path = System.Web.HttpContext.Current.Server.MapPath("~" + vehicle.PictureUrl);
            System.IO.File.WriteAllBytes(path, vehicle.Picture);
        }
    }
}