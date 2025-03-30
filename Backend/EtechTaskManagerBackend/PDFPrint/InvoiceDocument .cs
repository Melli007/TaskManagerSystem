using EtechTaskManagerBackend.DTO;
using EtechTaskManagerBackend.Models;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Azure;
using EtechTaskManagerBackend.Interfaces;
using SkiaSharp;
using System.Text;
using Microsoft.VisualBasic;

namespace ETechTaskManager.PDFPrint
{
    public class InvoiceDocument : IDocument
    {
        public List<InvoiceItem> Items { get; set; }
        public InvoiceSummary Summary { get; set; }
        public InvoiceItem UserInfo { get; set; }
        public InvoiceDocument(List<InvoiceItem> items, InvoiceSummary summary, InvoiceItem userInfo)
        {
            Items = items;
            Summary = summary;
            UserInfo = userInfo;
        }
        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Margin(20); page.Size(PageSizes.A4); page.Content().Column(column =>
                {
                    // Header Section
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(headerColumn =>
                        {
                            headerColumn.Item().Text($"Raporti i performancës").FontSize(24).Bold().FontColor(Colors.Blue.Medium); 
                            headerColumn.Item().PaddingBottom(25).Text($"Data: {DateTime.Now:dd.MM.yyyy}").FontSize(12).FontColor(Colors.Grey.Darken2);
                        });
                        
                        row.ConstantItem(100).Height(60).Image("C:\\Users\\Asus\\source\\repos\\ETechTaskManager\\Frontend\\ETechTaskManager\\wwwroot\\Images\\logo.png");
                    });


                    column.Item().PaddingTop(20).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
                    {
                        row.RelativeItem().Column(Info =>
                        {
                            Info.Spacing(5); Info.Item().Column(col =>
                            {
                                col.Item().Text("Informacioni i Përdoruesit").SemiBold().FontSize(16).FontColor(Colors.Blue.Medium); 
                                col.Item().Height(1).Width(200).Background(Colors.Black);
                            }); 

                            Info.Item().Text($"Emri: {UserInfo.AssignedToName}");
                            Info.Item().Text($"Roli ne kompani: {UserInfo.Profession}"); 
                            Info.Item().Text($"Emaili: {UserInfo.Email}");
                            Info.Item().PaddingBottom(10).Text($"Numri telefonit: {UserInfo.Number}");
                        });
                    });

                    // Performance Section
                    column.Item().MaxHeight(330).Background("#F9F9F9").PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().PaddingTop(45).PaddingLeft(30).Column(performanceColumn =>
                        {
                            performanceColumn.Spacing(14);
                            performanceColumn.Item().Text("Statistikat e Performancës").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                            performanceColumn.Item().Text($"Totali i Taskeve: {Summary.TotalTasks}").FontSize(14).Italic();
                            performanceColumn.Item().Text($"Tasket e Perfunduara: {Summary.CompletedTasks}").FontSize(14).Italic();
                            performanceColumn.Item().Text($"Tasket në Progres: {Summary.InProgressTasks}").FontSize(14).Italic();
                            performanceColumn.Item().Text($"Tasket në Pritje: {Summary.PendingTasks}").FontSize(14).Italic();
                            performanceColumn.Item().Text($"Tasket e Vonuara: {Summary.TotalTasks - (Summary.CompletedTasks + Summary.InProgressTasks + Summary.PendingTasks)}").FontSize(14).Italic();
                            performanceColumn.Item().Text($"Performanca: {Summary.PerformanceScore:F2}%").FontSize(14).Italic();
                        });

                        row.RelativeItem().SkiaSharpRasterized((canvas, size) =>
                        {
                            var chartData = new Dictionary<string, int>
                            {
                                {"Të Perfunduara", Summary.CompletedTasks},
                                {"Në Progres", Summary.InProgressTasks},
                                {"Në Pritje", Summary.PendingTasks},
                                {"Të Vonuara", Summary.TotalTasks - (Summary.CompletedTasks + Summary.InProgressTasks + Summary.PendingTasks)}
                            };

                            DrawPieChartWithArrows(canvas, size, chartData);
                        });
                    });

                    // Table Section
                    column.Item().PaddingTop(20).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(4); 
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        }); 
                        
                        // Table Header
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Nr").SemiBold(); 
                            header.Cell().Element(CellStyle).Text("Titulli").SemiBold();
                            header.Cell().Element(CellStyle).Text("Përshkrimi").SemiBold();
                            header.Cell().Element(CellStyle).Text("Statusi").SemiBold();
                            header.Cell().Element(CellStyle).Text("Data e Afatit").SemiBold();
                            
                            static IContainer CellStyle(IContainer container)
                            {
                                return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                            }
                        });
                        
                        int counter = 1;
                        foreach (var item in Items)
                        {
                            table.Cell().Element(CellStyle).Text(counter.ToString());
                            table.Cell().Element(CellStyle).Text(item.Title); 
                            table.Cell().Element(CellStyle).Text($"{item.Description}");
                            table.Cell().Element(CellStyle).Text(item.Status);
                            table.Cell().Element(CellStyle).Text(item.DueDate?.ToString("dd.MM.yyyy") ?? "No Due Date"); 
                            counter++;
                        }

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                        }

                    });                  
                    
                });
                
                page.Footer().AlignCenter().PaddingTop(5).Text(x =>
                {
                    x.Span("E-Tech Task Manager - Raporti i Performancës | "); 
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }

        private void DrawPieChartWithArrows(SKCanvas canvas, Size size, Dictionary<string, int> data)
        {
            var total = data.Values.Sum();
            var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
            var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 14,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center // This centers the text horizontally
            };

            float startAngle = 0;
            var rect = new SKRect(40, 50, 240, 250); 
            var centerX = rect.MidX;
            var centerY = rect.MidY;
            var radius = rect.Width / 2;

            foreach (var kvp in data)
            {

                // Skip the drawing if the value is 0
                if (kvp.Value == 0)
                    continue;

                var sweepAngle = 360f * ((float)kvp.Value / total);

                // Draw the pie slice
                paint.Color = SKColor.Parse(GetRandomColor());
                canvas.DrawArc(rect, startAngle, sweepAngle, true, paint);

                // Calculate the midpoint of the arc for the arrow
                var midAngle = startAngle + sweepAngle / 2;
                var radians = Math.PI * midAngle / 180;

                // Arrow start (just outside the pie chart) and end points
                var arrowStartX = centerX + (float)(radius * Math.Cos(radians));
                var arrowStartY = centerY + (float)(radius * Math.Sin(radians));
                var arrowEndX = centerX + (float)((radius + 20) * Math.Cos(radians));
                var arrowEndY = centerY + (float)((radius + 20) * Math.Sin(radians));

                // Draw the arrow
                var arrowPaint = new SKPaint { Color = paint.Color, StrokeWidth = 2, IsAntialias = true };
                canvas.DrawLine(arrowStartX, arrowStartY, arrowEndX, arrowEndY, arrowPaint);

                // Calculate text position (centered on the arrow's end)
                var textX = arrowEndX;
                var textY = arrowEndY;

                // Save the canvas state before rotating the text
                canvas.Save();

                // Set rotation angle
                float textRotationAngle = midAngle + 90; // Default rotation of 90 degrees

                if (kvp.Key == "Të Perfunduara")
                {
                     textY = textY + 8;
                    textRotationAngle = 360; // Set rotation to 180 degrees for "Të Perfunduara"
                }

                // Rotate the canvas to match the arrow's direction
                canvas.RotateDegrees(textRotationAngle, textX, textY);

                // Draw the text at the center of the arrow's end
                canvas.DrawText(kvp.Key, textX, textY, textPaint);

                // Restore the canvas to its original state (to stop rotating for the next iteration)
                canvas.Restore();

                // Update start angle for the next slice
                startAngle += sweepAngle;
            }

            canvas.Flush();
        }

        private string GetRandomColor()
        {
            var random = new Random(); return $"#{random.Next(0x1000000):X6}";
        }

    }

    public class InvoiceItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime? DueDate { get; set; }
        public string AssignedToName { get; set; }
        public string Profession { get; set; }
        public string Email { get; set; }
        public string Number { get; set; }
    }

    public class InvoiceSummary
    {
        public int Id { get; set; }
        public string Comments { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int PendingTasks { get; set; }
        public int overdueTasks { get; set; }
        public double PerformanceScore { get; set; }
    }

    public static class SkiaSharpHelpers
    {
        public static void SkiaSharpCanvas(this IContainer container, Action<SKCanvas, Size> drawOnCanvas)
        {
            container.Svg(size =>
            {
                using var stream = new MemoryStream(); 
                using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, size.Width, size.Height), stream)) drawOnCanvas(canvas, size);
                var svgData = stream.ToArray(); return Encoding.UTF8.GetString(svgData);
            });
        }
        public static void SkiaSharpRasterized(this IContainer container, Action<SKCanvas, Size> drawOnCanvas)
        {
            container.Image(payload =>
            {
                using var bitmap = new SKBitmap(payload.ImageSize.Width, payload.ImageSize.Height); using (var canvas = new SKCanvas(bitmap))
                {
                    var scalingFactor = payload.Dpi / (float)DocumentSettings.DefaultRasterDpi;
                    canvas.Scale(scalingFactor); drawOnCanvas(canvas, payload.AvailableSpace);
                }
                return bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
            });
        }
    }


    [ApiController]
    [Route("api/[controller]")]
    public class PdfController : ControllerBase
    {
        private readonly ITasksRepository _tasksRepository;
        private readonly IUsersRepository _usersRepository;

        public PdfController(ITasksRepository tasksRepository, IUsersRepository usersRepository)
        {
            _tasksRepository = tasksRepository;
            _usersRepository = usersRepository;
        }

        [HttpPost("GenerateInvoice/{userId}")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<TasksDTO>))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GenerateInvoice(int userId)
        {
            try
            {
                // Fetch tasks for the user directly from the repository
                var tasks = _tasksRepository.GetTasksAssignedToUser(userId)
                .Where(t => t.CreatedBy != t.AssignedTo) // If needed
                .ToList();

                var users = _usersRepository.GetUserById(userId);

                if (tasks == null || !tasks.Any())
                {
                    return BadRequest("No tasks found for the specified user.");
                }

                if (users == null)
                {
                    return BadRequest("No information found for the specified user.");
                }

                // Map tasks to InvoiceItem
                var items = tasks.Select(task => new InvoiceItem
                {
                    Title = task.Title,
                    Description = task.Description,
                    Status = task.Status,
                    DueDate = task.DueDate
                }).ToList();

                var userInfo = new InvoiceItem
                {
                    AssignedToName = users.FullName,
                    Profession = users.Profession,
                    Email = users.Email,
                    Number = users.Phone
                };

                // Calculate task counts based on statuses
                int completedTasks = tasks.Count(t => t.Status == "Të Përfunduara");
                int inProgressTasks = tasks.Count(t => t.Status == "Në Progres");
                int pendingTasks = tasks.Count(t => t.Status == "Në Pritje");
                int overdueTasks = tasks.Count(t => t.Status == "Të Vonuara");
                int totalTasks = tasks.Count();

                // Optional: Calculate performance score based on completed tasks (could be adjusted)
                double performanceScore = totalTasks == 0 ? 0 : ((completedTasks * 1.0) + (inProgressTasks * 0.5) - (overdueTasks * 0.75)) / (double)(totalTasks) * 100;

                // Create Invoice Summary
                var summary = new InvoiceSummary
                {
                    Id = userId,
                    Comments = "Generated by ETechTaskManager",
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    InProgressTasks = inProgressTasks,
                    PendingTasks = pendingTasks,
                    PerformanceScore = Math.Max(0, performanceScore) // Ensure performance score is non-negative
                };

                // Generate PDF
                var document = new InvoiceDocument(items, summary, userInfo);
                var pdfBytes = document.GeneratePdf();

                return File(pdfBytes, "application/pdf", $"Invoice_User_{userId}.pdf");
            }
            catch (HttpRequestException httpEx)
            {
                return StatusCode(500, $"HttpRequestException: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
