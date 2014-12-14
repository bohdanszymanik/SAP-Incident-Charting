#r @"..\packages\NodaTime.1.3.0\lib\net35-Client\NodaTime.dll"
open NodaTime

SystemClock.Instance.Now

let pattern = NodaTime.Text.LocalDateTimePattern.CreateWithInvariantCulture("MM/dd/yyyy HH:mm")
pattern.Parse("11/05/2014 21:15")

let t = "15.01.2015" |> NodaTime.Text.LocalDateTimePattern.CreateWithInvariantCulture("dd.MM.yyyy").Parse
t.Success

let txtToDate = NodaTime.Text.LocalDateTimePattern.CreateWithInvariantCulture("dd.MM.yyyy").Parse

//
// Approach 1 - use the FSharp.Data CsvProvider and manipulate line by line
//
#r @"..\packages\FSharp.Data.2.1.0\lib\net40\FSharp.Data.dll"
open FSharp.Data

type iTP = CsvProvider<Sample = "incidents.tsv", Separators = "\t", HasHeaders = true>

let incidents = iTP.Load("incidents.tsv")

let firstLine = 
    incidents.Rows |> Seq.head

let iHistoryRaw = incidents.Rows
                |> Seq.map (fun i ->
                i.``Incident Number``, i.Priority , i.``Changed on`` - i.``Created on`` , i.``Auto Confirm Date`` , txtToDate i.``Auto Confirm Date``)

// maybe later on it's easier if we type the information in the tuple
type Incident = Incident of int
type Priority = High | Medium | Low
type DurationActive = DurationActive of System.TimeSpan
type AutoConfirm = AutoConfirm of Text.ParseResult<LocalDateTime>

let matchPriority p = match p with
                        | "High" -> High
                        | "Medium" -> Medium
                        | _ -> Low

let iHistory = incidents.Rows
                |> Seq.map (fun i ->
                i.``Incident Number`` |> Incident, 
                i.Priority |> matchPriority,
                i.``Changed on`` - i.``Created on`` |> DurationActive, 
                txtToDate i.``Auto Confirm Date`` |> AutoConfirm)


// we want a histogram of days to resolution for items, ideally split out by priority: high, medium and low
//#r @"..\packages\MathNet.Numerics.3.3.0\lib\net40\MathNet.Numerics.dll"
//#r @"..\packages\MathNet.Numerics.FSharp.3.3.0\lib\net40\MathNet.Numerics.FSharp.dll"
// save on typing in references and just use the helper below...
#load @"..\packages\MathNet.Numerics.FSharp.3.3.0\MathNet.Numerics.fsx"

// oooh, luv that pattern matching deconstruction :)
let priorityHistory priority = iHistory 
                                |> Seq.filter ( fun (i,p,t,_) -> p = priority ) 
                                |> Seq.map (fun (_,_,(DurationActive d),_) -> d.TotalDays)

let hiHistory = priorityHistory High
let medHistory = priorityHistory Medium
let lowHistory = priorityHistory Low

lowHistory |> Seq.iter (fun i -> printfn "%A" i)

open MathNet.Numerics.Statistics

// FSharp.Charting column chart doesn't display all labels if there's more than 10
// using underlying DataVisualization.Charting stacked chart types the number of x's must be the same
// across all series - quite reasonable
let hiHistogram = new Histogram(hiHistory, 10, -0.1, 120.)
let medHistogram = new Histogram(medHistory, 10, -0.1, 120.)
let lowHistogram = new Histogram(lowHistory, 10, -0.1, 120.)

let enumerateHistCount (hist:Histogram) = 
    seq{ for i = 0 to int hist.BucketCount - 1 do
         yield  ( 
                    (sprintf "%i to %i" ( int (hist.Item i).LowerBound) (int (hist.Item i).UpperBound) ) , 
                    (hist.Item i).Count 
                 ) }

// strange - doesn't like the lower bound = the minimum value ie 0. in this case - need to adjust lower bound to be a tiny bit lower
//let t = new Histogram([0.;4.;55.;6.;77.;88.], 6, -0.1, 120.)

(*
#load @"..\packages\FSharp.Charting.0.90.7\FSharp.Charting.fsx"
open FSharp.Charting

Chart.Column (enumerateHistCount hiHistogram)
*)
// actually FSharp charting is a pain - the underlying microsoft chart control auto fits the labels and it's just not that nice
// so work with underlying charting control instead...

#r "System.Windows.Forms.DataVisualization.dll"
open System
open System.Windows.Forms
open System.Windows.Forms.DataVisualization.Charting

let chart = new Chart(Dock = DockStyle.Fill)

let area = new ChartArea("Main")
area.AxisX.Minimum <- 0.
area.AxisX.Interval <- 1.;
area.AxisX.IsStartedFromZero <- true;
chart.ChartAreas.Add(area)

let mainForm = new Form(Visible = true, TopMost = true, 
                        Width = 700, Height = 500)
mainForm.Controls.Add(chart)

// Create series and add it to the chart with axis labels
let hiSeries = new Series("hiSeries")
hiSeries.ChartType <- SeriesChartType.StackedColumn
chart.Series.Add(hiSeries)

let medSeries = new Series("medSeries")
medSeries.ChartType <- SeriesChartType.StackedColumn
chart.Series.Add(medSeries)

enumerateHistCount hiHistogram
|> Seq.iteri (fun i (l, v) ->
    let dp = new DataPoint(float i, v)
    dp.AxisLabel <- l
    hiSeries.Points.Add dp
    )

enumerateHistCount medHistogram
|> Seq.iteri (fun i (l, v) ->
    let dp = new DataPoint(float i, v)
    dp.AxisLabel <- l
    medSeries.Points.Add dp
    )

chart.Legends.Add("ALegend")

// is this really necessary now???
chart.ChartAreas.[0].AxisX.Interval <- 0.


medHistory |> Seq.iter (fun i -> printfn "%A" i)
medHistogram
enumerateHistCount hiHistogram |> Seq.iter (fun (a,b) -> printfn "%A %A" a b)
for i = 0 to int medHistogram.BucketCount - 1 do
        printfn "%A" i
        printfn "%A %A"   (sprintf "%i to %i" (int (medHistogram.Item i).LowerBound) (int (medHistogram.Item i).UpperBound) )  (medHistogram.Item i).Count 



//
// Approach 2 - use Deedle and work in a dataframe
//




