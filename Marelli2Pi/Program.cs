using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Marelli2Pi
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Check argumants and open files
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: Marelli2Pi <data_file>");
                return;
            }
            if (!File.Exists(args[0]))
            {
                Console.WriteLine("File not found: " + args[0]);
                return;
            }

            string infile = args[0];
            string outfile = args[0] + ".txt";

            string units = "[ ]"; // Default units

            int timeFactor = 1000; // convert time field to ms

            // Read all lines from the CSV file
            var lines = File.ReadAllLines(infile);

            // Split each line into fields
            var rows = lines.Select(line => line.Split(',')).ToList();

            // Transpose rows to columns
            int columnCount = rows[0].Length;
            var columns = new List<List<string>>();

            for (int col = 0; col < columnCount; col++)
            {
                var column = new List<string>();
                foreach (var row in rows)
                {
                    column.Add(row[col]);
                }
                columns.Add(column);
            }

            // Find the index of the "DistanceLap" column header
            int lapColIndex = columns.FindIndex(col => col[0].Trim().Equals("DistanceLap", StringComparison.OrdinalIgnoreCase));

            int numLaps = 0; //Number of laps in the outing, needed to allocate memory for lap markers

            if (lapColIndex == -1)
            {
                Console.WriteLine("Error: 'DistanceLap' column not found.");
                return;
            }

            // Count the number of laps by checking the "DistanceLap" column
            for (int j = 1; j < columns[lapColIndex].Count; j++)
            {
                if (j == 1) continue; //skip the first row for comparision against the previous header row
                if (double.TryParse(columns[lapColIndex][j], out double currentLapValue) &&
                    double.TryParse(columns[lapColIndex][j - 1], out double previousLapValue))
                {
                    //find the first lap starting point
                    if ((columns[lapColIndex][j] == "0") && (numLaps == 0))
                    {
                        continue; //keep incrementing until we get to the start of the first lap
                    }

                    //Get double value from string
                    if (double.TryParse(columns[0][j], out double value))
                    {

                        //we've found the start of a lap, now we can record the lap marker
                        if (numLaps == 0)
                        {
                            numLaps++;
                            continue;
                        }

                        //add laps past the first lap
                        if (currentLapValue < previousLapValue)
                        {
                            numLaps++;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error parsing time {columns[0][j]}");
                        continue;
                    }
                }
            }

            TimeSpan[] lapMarkers = new TimeSpan[numLaps];
            lapMarkers[0] = TimeSpan.Zero; // Start with the first lap at zero
            int lapCounter = 0;


            // Write the Pi file header
            var writer = new StreamWriter(outfile, false, Encoding.GetEncoding(1252));      //Important to use the 1252 encoding to match Pi Toolbox ASCII format      

            // File header
            writer.WriteLine("PiToolboxVersionedASCIIDataSet");
            writer.WriteLine("Version\t2");
            writer.WriteLine();
            writer.WriteLine("{OutingInformation}");
            writer.WriteLine($"CarName\tMarelli");
            writer.WriteLine("FirstLapNumber\t0");

            // Cycle through and create channel blocks
            for (int i = 1; i < columns.Count; i++)
            {
                writer.WriteLine();
                writer.WriteLine("{ChannelBlock}");
                writer.WriteLine($"Time\t{columns[i][0]}{units}");

                for (int j = 1; j < columns[i].Count; j++)
                {
                    columns[i][j] = columns[i][j].Replace("h", ""); // Remove 'h' if present
                    if (double.TryParse(columns[0][j], out double value))
                    {
                        writer.WriteLine($"{value/timeFactor}\t{columns[i][j]}");
                    }
                    else
                    {
                        // Handle non-numeric data (optional)
                        writer.WriteLine($"{columns[0][j]}\t{columns[i][j]}");
                    }
                }
            }

            //cycle back through to extract lap markers
            for (int j = 1; j < columns[lapColIndex].Count; j++)
            {
                if (j == 1) continue; //skip the first row for comparision against the previous header row
                if (double.TryParse(columns[lapColIndex][j], out double currentLapValue) &&
                    double.TryParse(columns[lapColIndex][j - 1], out double previousLapValue))
                {
                    //find the first lap starting point
                    if ((columns[lapColIndex][j] == "0") && (lapCounter == 0))
                    {
                        continue; //keep incrementing until we get to the start of the first lap
                    }

                    //Get double value from string
                    if (double.TryParse(columns[0][j], out double value))
                    {
                        //value = value / timeFactor; // Divide by time factor to get the correct time
                      
                        //we've found the start of a lap, now we can record the lap marker
                        if (lapCounter == 0)
                        {
                            lapMarkers[lapCounter] = TimeSpan.FromMilliseconds(value);
                            //Console.WriteLine($"Lap {lapCounter} starts at {lapMarkers[lapCounter]} on Row {j}");
                            lapCounter++;
                            continue;
                        }

                        if (currentLapValue < previousLapValue)
                        {
                            lapMarkers[lapCounter] = TimeSpan.FromMilliseconds(value);
                            //Console.WriteLine($"Lap {lapCounter} starts at {lapMarkers[lapCounter]} on Row {j}");
                            lapCounter++;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error parsing time {columns[0][j]}");
                        continue;
                    }

                }
            }

            // Event block for lap breakpoints
            writer.WriteLine();
            writer.WriteLine("{EventBlock}");
            writer.WriteLine("Time\tName\tCategory\tSource\tMessage");

            for (int idx = 0; idx < numLaps; ++idx)
            {
                writer.WriteLine($"{lapMarkers[idx].TotalSeconds}\tEnd of lap\tToolbox Added\tDRV\tEnd of lap");
            }

            writer.Close();
            Console.WriteLine($"Conversion complete. {numLaps} laps written to {outfile}");
        }
    }
}
