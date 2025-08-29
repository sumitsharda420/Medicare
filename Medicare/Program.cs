using System;
using System.Diagnostics;
using System.Text;
using Medicare;
using Microsoft.Data.SqlClient;
using org.apache.tika.detect;
using org.apache.tika.extractor;
using org.apache.tika.parser;
using org.apache.tika.parser.txt;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Graphics;
// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
QuestPDF.Settings.License = LicenseType.Community;
string connectionString = "Server=localhost;Database=us_dhvn_multi;Integrated Security=True;TrustServerCertificate=True;";
string baseOutputDir = @"C:\ExportedRecords";


Directory.CreateDirectory(baseOutputDir);

//var csvRows = new List<string>();
//csvRows.Add("ResidentName,MRN,Year,Pharmacy,Physician,Medication,Directions,ScheduleDate,ScheduleTime,PerformTime,AdministeredBy,ChartCode,FilePath");

//var emarRecords = LoadEmarRecords(connectionString);

//var groups = emarRecords.GroupBy(e => new { e.ResidentName, e.Year });

//foreach (var group in groups)
//{
//    string folder = Path.Combine(baseOutputDir, "Emar", group.Key.Year.ToString());
//    Directory.CreateDirectory(folder);

//    string safeName = group.Key.ResidentName.Replace(",", "").Replace(" ", "_");
//    string fileName = $"Emar_{safeName}_{group.Key.Year}.pdf";
//    string fullPath = Path.Combine(folder, fileName);

//    GenerateEmarPdf(fullPath, group.ToList());

//    csvRows.Add($"{group.Key.ResidentName},{group.First().MRN},{group.Key.Year},{group.First().Pharmacy},{group.First().Physician}," +
//                $"{group.First().Medication},{group.First().Directions},{group.First().ScheduleDate:MM/dd/yyyy}," +
//                $"{group.First().ScheduleTime},{group.First().PerformTime},{group.First().AdministeredBy}," +
//                $"{group.First().ChartCode},{fullPath}");

//    Console.WriteLine($"PDF saved: {fullPath}");
//}

//File.WriteAllLines(Path.Combine(baseOutputDir, "MasterIndex_Emar.csv"), csvRows, Encoding.UTF8);

var csvRows = new List<string>();
csvRows.Add("ResidentName,MRN,Year,Type,Pharmacy,Physician,Author,NoteText,EffectiveDate,MoveInDate,FilePath");
var notes = LoadProgressNotes(connectionString);

var groups = notes.GroupBy(n => new { n.ResidentName, n.Year });

foreach (var group in groups)
{
    string folder = Path.Combine(baseOutputDir, "PCC", group.Key.Year.ToString());
    Directory.CreateDirectory(folder);

    string safeName = group.Key.ResidentName.Replace(",", "").Replace(" ", "_");
    string fileName = $"PCC_{safeName}_{group.Key.Year}.pdf";
    string fullPath = Path.Combine(folder, fileName);

    GeneratePdf(fullPath, group.ToList());

    csvRows.Add($"{group.Key.ResidentName},{group.First().MRN},{group.Key.Year},{group.First().Pharmacy},{group.First().Physician},{group.First().Author},{group.First().NoteText},{group.First().EffectiveDate},{group.First().admission_date}{fullPath}");

    Console.WriteLine($"PDF saved: {fullPath}");
}
File.WriteAllLines(Path.Combine(baseOutputDir, "MasterIndex.csv"), csvRows, Encoding.UTF8);
//ExportEMAR(connectionString, baseOutputDir, csvWriter);


static List<NoteRecord> LoadProgressNotes(string connStr)
{
    var list = new List<NoteRecord>();

    string query = @"
SELECT 
    pn.pn_id AS RecordId,
    mpi.last_name + ',' + mpi.first_name AS ResidentName,
    cl.client_id_number AS MRN,
    emc.name AS Pharmacy,
    cl.admission_date as admission_date,
    pn.effective_date AS EffectiveDate,
    YEAR(pn.effective_date) AS [Year],
    cont.last_name + ',' + cont.first_name AS Physician,
    ca.allergy AS Allergies,
    pnt.description AS [Type],
    pntext.text1 AS NoteText,
    pn.signed_by_fullname AS Author,
    u.unit_desc + ' ' + r.room_desc + ' ' + b.bed_desc AS Location
FROM clients AS cl
INNER JOIN mpi ON cl.mpi_id = mpi.mpi_id
INNER JOIN emc_ext_facilities AS emc ON cl.pharmacy_id = emc.ext_fac_id
INNER JOIN contact AS cont ON cl.primary_physician_id = cont.contact_id
INNER JOIN clients_attribute AS ca ON cl.client_id = ca.client_id 
INNER JOIN pn_progress_note AS pn ON cl.client_id = pn.client_id 
INNER JOIN pn_type AS pnt ON pn.pn_type_id = pnt.pn_type_id 
INNER JOIN pn_text AS pntext ON pn.pn_id = pntext.pn_id
LEFT JOIN census_item ci ON cl.current_census_id = ci.census_id
LEFT JOIN bed b ON ci.bed_id = b.bed_id
LEFT JOIN room r ON b.room_id = r.room_id
LEFT JOIN unit u ON r.unit_id = u.unit_id

ORDER BY pn.effective_date ASC;";

    using var conn = new SqlConnection(connStr);
    conn.Open();
    using var cmd = new SqlCommand(query, conn);
    cmd.CommandTimeout = 12000;
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        list.Add(new NoteRecord
        {
            RecordId = Convert.ToInt32(reader["RecordId"]),
            ResidentName = reader["ResidentName"].ToString(),
            MRN = reader["MRN"].ToString(),
            Pharmacy = reader["Pharmacy"].ToString(),
            EffectiveDate = Convert.ToDateTime(reader["EffectiveDate"]),
            admission_date = reader["admission_date"] == DBNull.Value
    ? DateTime.MinValue
    : Convert.ToDateTime(reader["admission_date"]),
            Year = Convert.ToInt32(reader["Year"]),
            Physician = reader["Physician"].ToString(),
            Allergies = reader["Allergies"].ToString(),
            Type = reader["Type"].ToString(),
            NoteText = reader["NoteText"].ToString(),
            Author = reader["Author"].ToString(),
            Location = reader["Location"].ToString()
        });
    }

    return list;
}

static void GeneratePdf(string path, List<NoteRecord> notes)
{
    var first = notes.First();

    var doc = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);

            page.Header().Column(col =>
            {
                col.Item().Text("Progress Notes").FontSize(18).Bold();
                col.Item().Text($"Resident Name: {first.ResidentName}");
                col.Item().Text($"Medical Record #: {first.MRN}");
                col.Item().Text($"Pharmacy: {first.Pharmacy}");
                col.Item().Text($"Physician: {first.Physician}");
                col.Item().Text($"Allergies: {first.Allergies}");
                col.Item().Text($"Location: {first.Location}");
            });

            page.Content().Column(col =>
            {
                foreach (var note in notes)
                {
                    col.Item().PaddingTop(10).BorderBottom(1).Column(inner =>
                    {
                        inner.Item().Text($"Effective Date:{note.EffectiveDate:MM/dd/yyyy}").Bold();
                        inner.Item().Text($"Move In Date:{note.admission_date:MM/dd/yyyy}").Bold();
                        inner.Item().Text($"Author: {note.Author}").Bold();
                        inner.Item().Text($"Type: {note.Type}");
                        inner.Item().Text($"Note Text:{ note.NoteText}").FontSize(11);
                    });
                }
            });

            page.Footer().AlignCenter().Text($"Generated on {DateTime.Now:MM/dd/yyyy HH:mm}");
        });
    });

    doc.GeneratePdf(path);
}
static List<EmarRecord> LoadEmarRecords(string connStr)
{
    var list = new List<EmarRecord>();

    string query = @"
    WITH EmarOrderTypes AS (
        SELECT order_type_id
        FROM pho_order_type
        WHERE administration_record_id = 1
          AND deleted = 'N'
    ),
    EmarOrders AS (
        SELECT po.phys_order_id,
               po.client_id,
               po.drug_name,
               po.directions,
               po.start_date,
               po.end_date
        FROM pho_phys_order po
        WHERE po.order_type_id IN (SELECT order_type_id FROM EmarOrderTypes)
    ),
    OrderSchedule AS (
        SELECT s.phys_order_id,
               sd.pho_schedule_detail_id,
               CAST(sd.schedule_date AS date) AS ScheduleDate,
               CONVERT(char(5), sd.schedule_date, 108) AS ScheduleTime,
               sd.perform_date,
               CONVERT(char(5), sd.perform_date, 108) AS PerformTime,
               sd.perform_initials,
               sd.chart_code
        FROM pho_schedule s
        INNER JOIN pho_schedule_details sd
            ON s.schedule_id = sd.pho_schedule_id
        WHERE sd.deleted = 'N'
    )
    SELECT 
        mpi.last_name + ', ' + mpi.first_name AS ResidentName,
        cl.client_id_number AS MRN,
        fac.name AS FacilityName,
        emc.name AS Pharmacy,
        cont.last_name + ', ' + cont.first_name AS Physician,
        ca.allergy AS Allergies,
        eo.drug_name AS Medication,
        eo.directions AS Directions,
        os.ScheduleDate,
        os.ScheduleTime,
        os.PerformTime,
        os.perform_initials AS AdministeredBy,
        os.chart_code AS ChartCode,
        YEAR(os.ScheduleDate) AS [Year]
    FROM EmarOrders eo
    INNER JOIN OrderSchedule os
        ON eo.phys_order_id = os.phys_order_id
    INNER JOIN clients cl
        ON eo.client_id = cl.client_id
        AND cl.deleted = 'N'
    INNER JOIN mpi 
        ON cl.mpi_id = mpi.mpi_id
    INNER JOIN clients_attribute AS ca 
        ON cl.client_id = ca.client_id
    LEFT JOIN facility fac
        ON cl.fac_id = fac.fac_id
    LEFT JOIN emc_ext_facilities emc 
        ON cl.pharmacy_id = emc.ext_fac_id
    LEFT JOIN contact cont 
        ON cl.primary_physician_id = cont.contact_id
where cl.fac_id=5
    ORDER BY ResidentName, os.ScheduleDate, os.ScheduleTime;";

    using var conn = new SqlConnection(connStr);
    conn.Open();
    using var cmd = new SqlCommand(query, conn);
    cmd.CommandTimeout = 12000;
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        list.Add(new EmarRecord
        {
            ResidentName = reader["ResidentName"].ToString(),
            MRN = reader["MRN"].ToString(),
            FacilityName = reader["FacilityName"]?.ToString(),
            Pharmacy = reader["Pharmacy"]?.ToString(),
            Physician = reader["Physician"]?.ToString(),
            Allergies = reader["Allergies"]?.ToString(),
            Medication = reader["Medication"]?.ToString(),
            Directions = reader["Directions"]?.ToString(),
            ScheduleDate = reader["ScheduleDate"] != DBNull.Value
                ? Convert.ToDateTime(reader["ScheduleDate"])
                : DateTime.MinValue,
            ScheduleTime = reader["ScheduleTime"]?.ToString(),
            PerformTime = reader["PerformTime"]?.ToString(),
            AdministeredBy = reader["AdministeredBy"]?.ToString(),
            ChartCode = reader["ChartCode"]?.ToString(),
            Year = Convert.ToInt32(reader["Year"])
        });
    }

    return list;
}
static void GenerateEmarPdf(string path, List<EmarRecord> records)
{
    var first = records.First();

    var doc = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);

            page.Header().Column(col =>
            {
                col.Item().Text("Medication Administration Record (eMAR)").FontSize(18).Bold();
                col.Item().Text($"Community: {first.FacilityName}");
                col.Item().Text($"Resident Name: {first.ResidentName}");
                col.Item().Text($"Medical Record #: {first.MRN}");
                col.Item().Text($"Pharmacy: {first.Pharmacy}");
                col.Item().Text($"Physician: {first.Physician}");
                col.Item().Text($"Allergies: {first.Allergies}");
            });

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);  // Sr
                    columns.RelativeColumn(2);   // Medication
                    columns.RelativeColumn(3);   // Directions
                    columns.ConstantColumn(70);  // Schedule Date
                    columns.ConstantColumn(50);  // Sch Time
                    columns.ConstantColumn(50);  // Perf Time
                    columns.ConstantColumn(50);  // Admin By
                });

                // Header Row
                table.Header(header =>
                {
                    header.Cell().Text("#").Bold();
                    header.Cell().Text("Medication").Bold();
                    header.Cell().Text("Directions").Bold();
                    header.Cell().Text("Date").Bold();
                    header.Cell().Text("Schedule Time").Bold();
                    header.Cell().Text("Perform Time").Bold();
                    header.Cell().Text("Admin By").Bold();
                });

                int i = 1;
                foreach (var rec in records)
                {
                    table.Cell().Text(i++.ToString());
                    table.Cell().Text(rec.Medication ?? "");
                    table.Cell().Text(rec.Directions ?? "");
                    table.Cell().Text(rec.ScheduleDate.ToString("MM/dd/yyyy"));
                    table.Cell().Text(rec.ScheduleTime ?? "");
                    table.Cell().Text(rec.PerformTime ?? "");
                    table.Cell().Text(rec.AdministeredBy ?? "");
                }
            });

            page.Footer().AlignCenter().Text($"Generated on {DateTime.Now:MM/dd/yyyy HH:mm}");
        });
    });

    doc.GeneratePdf(path);
}
static string Quote(object val)
{
    return "\"" + val?.ToString().Replace("\"", "\"\"") + "\"";
}
public class EmarRecord
{
    public string ResidentName { get; set; }
    public string MRN { get; set; }
    public string Pharmacy { get; set; }
    public string Physician { get; set; }
    public string Medication { get; set; }
    public string Directions { get; set; }
    public DateTime ScheduleDate { get; set; }
    public string ScheduleTime { get; set; }
    public string PerformTime { get; set; }
    public string AdministeredBy { get; set; }
    public string ChartCode { get; set; }
    public int Year { get; set; }
    public string FacilityName { get; set; }
    public string Allergies { get; set; }
}

public class NoteRecord
{
    public int RecordId { get; set; }
    public string ResidentName { get; set; }
    public string MRN { get; set; }
    public string Pharmacy { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime admission_date { get; set; }
    public int Year { get; set; }
    public string Physician { get; set; }
    public string Allergies { get; set; }
    public string Type { get; set; }
    public string NoteText { get; set; }
    public string Author { get; set; }
    public string Location { get; set; }
}
