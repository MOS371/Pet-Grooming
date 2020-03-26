﻿using System;
using System.Collections.Generic;
using System.Data;
//required for SqlParameter class
using System.Data.SqlClient;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using PetGrooming.Data;
using PetGrooming.Models;
using PetGrooming.Models.ViewModels;
using System.Diagnostics;
using System.IO;

namespace PetGrooming.Controllers
{
    public class PetController : Controller
    {
        /*
        These reading resources will help you understand and navigate the MVC environment
 
        Q: What is an MVC controller?

        - https://docs.microsoft.com/en-us/aspnet/mvc/overview/older-versions-1/controllers-and-routing/aspnet-mvc-controllers-overview-cs

        Q: What does it mean to "Pass Data" from the Controller to the View?

        - http://www.webdevelopmenthelp.net/2014/06/using-model-pass-data-asp-net-mvc.html

        Q: What is an SQL injection attack?

        - https://www.w3schools.com/sql/sql_injection.asp

        Q: How can we prevent SQL injection attacks?

        - https://www.completecsharptutorial.com/ado-net/insert-records-using-simple-and-parameterized-query-c-sql.php

        Q: How can I run an SQL query against a database inside a controller file?

        - https://www.entityframeworktutorial.net/EntityFramework4.3/raw-sql-query-in-entity-framework.aspx
 
         */
        private PetGroomingContext db = new PetGroomingContext();

        // GET: Pet
        public ActionResult List(string petsearchkey)
        {
            //can we access the search key?
            Debug.WriteLine("The search key is "+petsearchkey);

            string query = "Select * from Pets";

            if (petsearchkey!="")
            {
                //modify the query to include the search key
                query = query + " where petname like '%"+petsearchkey+"%'";
                Debug.WriteLine("The query is "+ query);
            }

            List<Pet> pets = db.Pets.SqlQuery(query).ToList();
            return View(pets);
           
        }

        // GET: Pet/Details/5
        public ActionResult Show(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            // Pet pet = db.Pets.Find(id); //EF 6 technique
            Pet Pet = db.Pets.SqlQuery("select * from pets where petid=@PetID", new SqlParameter("@PetID",id)).FirstOrDefault();
            if (Pet == null)
            {
                return HttpNotFound();
            }

            //need information about the list of owners associated with that pet
            string query = "select * from owners inner join PetOwners on Owners.OwnerID = PetOwners.Owner_OwnerID where Pet_PetID = @id";
            SqlParameter param = new SqlParameter("@id", id);
            List<Owner> PetOwners = db.Owners.SqlQuery(query, param).ToList();

            //need information about the grooms booked for this pet
            //note: would do a standard join to get the owner of this booking
            //however, cannot force this information into the model
            //must use linq in this situation
            List<GroomBooking> BookedGrooms = db.GroomBookings.Include(b=>b.Owner).ToList();



            ShowPet viewmodel = new ShowPet();
            viewmodel.pet = Pet;
            viewmodel.owners = PetOwners;
            viewmodel.bookedgrooms = BookedGrooms;


            return View(viewmodel);
        }

        //THE [HttpPost] Means that this method will only be activated on a POST form submit to the following URL
        //URL: /Pet/Add
        [HttpPost]
        public ActionResult Add(string PetName, Double PetWeight, String PetColor, int SpeciesID, string PetNotes)
        {
            //STEP 1: PULL DATA! The data is access as arguments to the method. Make sure the datatype is correct!
            //The variable name  MUST match the name attribute described in Views/Pet/Add.cshtml

            //Tests are very useul to determining if you are pulling data correctly!
            //Debug.WriteLine("Want to create a pet with name " + PetName + " and weight " + PetWeight.ToString()) ;

            //STEP 2: FORMAT QUERY! the query will look something like "insert into () values ()"...
            string query = "insert into pets (PetName, Weight, color, SpeciesID, Notes) values (@PetName,@PetWeight,@PetColor,@SpeciesID,@PetNotes)";
            SqlParameter[] sqlparams = new SqlParameter[5]; //0,1,2,3,4 pieces of information to add
            //each piece of information is a key and value pair
            sqlparams[0] = new SqlParameter("@PetName",PetName);
            sqlparams[1] = new SqlParameter("@PetWeight", PetWeight);
            sqlparams[2] = new SqlParameter("@PetColor", PetColor);
            sqlparams[3] = new SqlParameter("@SpeciesID", SpeciesID);
            sqlparams[4] = new SqlParameter("@PetNotes",PetNotes);

            //db.Database.ExecuteSqlCommand will run insert, update, delete statements
            //db.Pets.SqlCommand will run a select statement, for example.
            db.Database.ExecuteSqlCommand(query, sqlparams);

            
            //run the list method to return to a list of pets so we can see our new one!
            return RedirectToAction("List");
        }


        public ActionResult New()
        {
            //STEP 1: PUSH DATA!
            //What data does the Add.cshtml page need to display the interface?
            //A list of species to choose for a pet

            //alternative way of writing SQL -- will learn more about this week 4
            //List<Species> Species = db.Species.ToList();

            List<Species> species = db.Species.SqlQuery("select * from Species").ToList();

            return View(species);
        }

        public ActionResult Update(int id)
        {
            //need information about a particular pet
            Pet selectedpet = db.Pets.SqlQuery("select * from pets where petid = @id", new SqlParameter("@id",id)).FirstOrDefault();
            List<Species> Species = db.Species.SqlQuery("select * from species").ToList();

            UpdatePet UpdatePetViewModel = new UpdatePet();
            UpdatePetViewModel.Pet = selectedpet;
            UpdatePetViewModel.Species = Species;

            return View(UpdatePetViewModel);
        }

        [HttpPost]
        public ActionResult Update(int id, string PetName, string PetColor, double PetWeight, string PetNotes, int SpeciesID, HttpPostedFileBase PetPic)
        {
            //start off with assuming there is no picture

            int haspic = 0;
            string petpicextension = "";
            //checking to see if some information is there
            if (PetPic != null)
            {
                Debug.WriteLine("Something identified...");
                //checking to see if the file size is greater than 0 (bytes)
                if (PetPic.ContentLength > 0)
                {
                    Debug.WriteLine("Successfully Identified Image");
                    //file extensioncheck taken from https://www.c-sharpcorner.com/article/file-upload-extension-validation-in-asp-net-mvc-and-javascript/
                    var valtypes = new[] { "jpeg", "jpg", "png", "gif" };
                    var extension = Path.GetExtension(PetPic.FileName).Substring(1);

                    if (valtypes.Contains(extension))
                    {
                        try { 
                            //file name is the id of the image
                            string fn = id + "." + extension;

                            //get a direct file path to ~/Content/Pets/{id}.{extension}
                            string path = Path.Combine(Server.MapPath("~/Content/Pets/"), fn);

                            //save the file
                            PetPic.SaveAs(path);
                            //if these are all successful then we can set these fields
                            haspic = 1;
                            petpicextension = extension;

                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine("Pet Image was not saved successfully.");
                            Debug.WriteLine("Exception:"+ex);
                        }



                    }
                }
            }

            //Debug.WriteLine("I am trying to edit a pet's name to "+PetName+" and change the weight to "+PetWeight.ToString());

            string query = "update pets set PetName=@PetName, SpeciesID=@SpeciesID, Weight=@PetWeight, color=@color, Notes=@Notes, HasPic=@haspic, PicExtension=@petpicextension where PetID=@id";
            SqlParameter[] sqlparams = new SqlParameter[8];
            sqlparams[0] = new SqlParameter("@PetName", PetName);
            sqlparams[1] = new SqlParameter("@PetWeight", PetWeight);
            sqlparams[2] = new SqlParameter("@color", PetColor);
            sqlparams[3] = new SqlParameter("@SpeciesID", SpeciesID);
            sqlparams[4] = new SqlParameter("@Notes", PetNotes);
            sqlparams[5] = new SqlParameter("@id",id);
            sqlparams[6] = new SqlParameter("@HasPic", haspic);
            sqlparams[7] = new SqlParameter("@petpicextension",petpicextension);

            db.Database.ExecuteSqlCommand(query, sqlparams);

            //logic for updating the pet in the database goes here
            return RedirectToAction("List");
        }
      
        public ActionResult DeleteConfirm(int id)
        {
            string query = "select * from pets where petid = @id";
            SqlParameter param = new SqlParameter("@id",id);
            Pet selectedpet = db.Pets.SqlQuery(query, param).FirstOrDefault();

            return View(selectedpet);
        }
        [HttpPost]
        public ActionResult Delete(int id)
        {
            string query = "delete from pets where petid = @id";
            SqlParameter param = new SqlParameter("@id", id);
            db.Database.ExecuteSqlCommand(query, param);

            return RedirectToAction("List");
        }

        //TODO:
        //Update
        //[HttpPost] Update
        //[HttpPost] Delete
        //(optional) Delete


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
