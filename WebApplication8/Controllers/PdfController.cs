using iText.IO.Source;
using iText.Kernel.Pdf.Event;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Text;
using WebApplication8.Data;
using WebApplication8.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NReco.PdfGenerator;
using System.Dynamic;

namespace WebApplication8.Controllers
{
    public class PdfController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ICompositeViewEngine _viewEngine;

        public PdfController(ApplicationDbContext dbContext, ICompositeViewEngine viewEngine)
        {
            _dbContext = dbContext;
            _viewEngine = viewEngine;
        }

        public async Task<IActionResult> Index()
        {
            // Fetch property values from the database
            var propertyValues = await _dbContext.PropertyValues.ToListAsync();

            // Create a dictionary to hold property-value pairs
            IDictionary<string, Tuple<string, string, object>> modelDictionary = new Dictionary<string, Tuple<string, string, object>>();

            foreach (var entry in propertyValues)
            {
                modelDictionary[entry.PropertyName] = new Tuple<string, string, object>(entry.PropertyName, entry.labeltype, entry.Value);
            }

            // Return the model dictionary to the view
            return View(modelDictionary);
        }

        // Method to render the view to string (HTML)
        private async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            ViewData.Model = model;
            using (var writer = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(ControllerContext, viewName, false);
                if (viewResult.View == null)
                {
                    throw new InvalidOperationException($"View '{viewName}' not found.");
                }

                var viewContext = new ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    writer,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return writer.ToString();
            }
        }

        // Post method for saving PDF from the view
        [HttpPost]
        public async Task<IActionResult> SavePdfFromView(IFormCollection formCollection)
        {
            var filteredFormCollection = formCollection
                .Where(kvp => kvp.Key != "__RequestVerificationToken")  // Exclude the 'requestToken' key
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            PropertyValue newProperty = new PropertyValue();
            // Loop through each form field (key-value pairs)
            foreach (var key in filteredFormCollection.Keys)
            {
                string propertyName = key;
                string updatedValue = formCollection[key];

                // Find the existing property record from the database
                newProperty = await _dbContext.PropertyValues
                    .FirstOrDefaultAsync(p => p.PropertyName == propertyName);

                if (newProperty != null)
                {
                    // Update the existing property with the new value
                    newProperty.Value = int.Parse(updatedValue);  // Assuming updatedValue is an integer
                    _dbContext.Update(newProperty);
                }
                else
                {
                    // If property doesn't exist, add a new record to the database
                    newProperty.PropertyName = propertyName;
                    newProperty.Value = int.Parse(updatedValue!);
                    _dbContext.Add(newProperty);
                }
            }
            await _dbContext.SaveChangesAsync();

            // Fetch the updated data to pass into the PDF generation method
            var model1 = await _dbContext.PropertyValues.ToListAsync();

            // Create a dictionary to hold property-value pairs
            IDictionary<string, Tuple<string, string, object>> modelDictionary = new Dictionary<string, Tuple<string, string, object>>();

            foreach (var entry in model1)
            {
                modelDictionary[entry.PropertyName] = new Tuple<string, string, object>(entry.PropertyName, entry.labeltype, entry.Value);
            }
            // Render the updated data to HTML string (pass modelDictionary instead of dynamic model)
            var htmlContent = await RenderViewToStringAsync("Index", modelDictionary); // Adjust 'Index' to match your template

            // Generate the PDF from the HTML content
            var pdfFile = DownloadPdfFromImage(htmlContent);

            // Return the PDF as a downloadable file
            return File(pdfFile, "application/pdf", "UpdatedData.pdf");
        }

        public byte[] DownloadPdfFromImage(string htmlContent)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent)))
            {
                using (var byteArrayOutputStream = new MemoryStream())
                {
                    var writer = new PdfWriter(byteArrayOutputStream);
                    var pdfDocument = new PdfDocument(writer);
                    // Use HtmlConverter to convert HTML to PDF
                    iText.Html2pdf.HtmlConverter.ConvertToPdf(stream, pdfDocument);
                    pdfDocument.Close();
                    return byteArrayOutputStream.ToArray();
                }
            }
        }
    }
}
