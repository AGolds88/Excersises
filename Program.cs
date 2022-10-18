using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MailListGroupAndSort
{
    internal class Program
    {
        static void Main()
        {
            // The reporting age requirement might change, and this hardcoding should be altered to accept user input or other source
            int occupantAgeRequirement = 18;

            Console.WriteLine("Authored by Andrew S. Goldstein.  Support questions? Call 425-829-3623 during 2:00 to 5:00 PM PST M-F.\n\n");
            // Display on the console, a brief introduction about this program
            Console.WriteLine("This program consumes a user supplied text file containing a list of mailing address.");
            Console.WriteLine("A list of households listing all occupants over the required age of {0} will be displayed.", occupantAgeRequirement);
            Console.WriteLine("Any errors in the supplied text files that are not in the correct format");
            Console.WriteLine("will be displayed after the tabularized data.\n\n");

            //Ask the user for a valid text file to process:
            string addressListTextFile = GetTextFileFromUser();

            //Generate an empty DataTable with the specified string fields.
            string[] columnNames = { "FirstName", "LastName", "Street", "City", "State", "Age" };
            DataTable holdingDataTable = CreateDataTable(columnNames);

            // Validate each line from the user supplied text file before adding to the DataTable.
            // Return a string with all invalid data (Empty if no invalid data populate the string with the invalid data.
            string invalidLinesFoundIfNotEmpty = ValidateTextFileLine(addressListTextFile, holdingDataTable);

            //// Display the contents of holdingDataTable ('intermediate' step for troubleshooting).
            //Console.WriteLine("\nThe text file contains the following valid {0} entries:\n", holdingDataTable.Rows.Count.ToString());

            //int fieldCounter = 0;
            //foreach (DataRow dataRow in holdingDataTable.Rows)
            //{
            //    foreach (var item in dataRow.ItemArray)
            //    {
            //        Console.Write("\t{0}", item);
            //        fieldCounter++;
            //        if (fieldCounter % 6 == 0) Console.WriteLine("\n");
            //    }
            //}

            // Display the final tabulated results.
            Console.WriteLine("\nFinalized Tabulation\n");
            Console.WriteLine("The unique combination of a street, city and state defines a household.\n ");
            Console.WriteLine("Only occupants age 19 or older will be displayed (Note that households could be listed\n");
            Console.WriteLine("without any occupants in some cases if none are older than {0} years of age)\n\n", occupantAgeRequirement);

            // Query the holdingDataTable
            Console.WriteLine("Displaying occupants older than age {0}:\n", occupantAgeRequirement);

            // To meet the required output requirements, sort the DataTable before pulling a query.
            // Note - In the future, make this into a method so that other sorting criteria 
            // could be implemented.
            DataView dataview = holdingDataTable.DefaultView;
            dataview.Sort = "Street, City, State, LastName, FirstName";
            holdingDataTable = dataview.ToTable();

            // Group holdingDataTable by Street + City + State
            // Note - In the future, make this into a method so that other grouping criteria
            // logic could be implemented.
            var groupedOccupants = from table in holdingDataTable.AsEnumerable()
                                   group table by new
                                   {
                                       placeCol1 = table["Street"],
                                       placeCol2 = table["City"],
                                       placeCol3 = table["State"]
                                   }
                                   into groupby
                                   select new
                                   {
                                       Value = groupby.Key,
                                       ColumnValues = groupby,
                                   };

            string houseHoldHeader = "";
            foreach (var key in groupedOccupants)
            {
                // Because of the requirement to include the number of occupants on the same line
                // of the household header, this requires generating several strings instead of 
                // a direct console output during the looping.  Once the strings are built, display their contents
                // to meet the output requirements.

                // Build the header string
                houseHoldHeader = key.Value.placeCol1 + " " + key.Value.placeCol2 + " " + key.Value.placeCol3;

                int allOccupantCounts = 0;
                string occupantNameAddressAge = "";
                foreach (var resident in key.ColumnValues)
                {
                    // Build the occupant string
                    allOccupantCounts++;
                    if (Int16.Parse(resident["Age"].ToString()) > occupantAgeRequirement)
                    {
                        occupantNameAddressAge += "\t" + resident["FirstName"].ToString() + " " +
                                                  resident["LastName"].ToString() + " " +
                                                  resident["Street"].ToString() + " " +
                                                  resident["City"].ToString() + " " +
                                                  resident["State"].ToString() + " - Age " +
                                                  resident["Age"].ToString() + "\n";
                    }
                }

                //Output to the console the households with the occupants as specified by the requirements.
                Console.WriteLine("{0} has {1} occupants (of all ages)", houseHoldHeader, allOccupantCounts.ToString());
                Console.WriteLine("{0}\n", new string('-', 65));
                Console.WriteLine(occupantNameAddressAge);
            }

            //Output to the console any invalid lines from the user supplied text file text file
            if (invalidLinesFoundIfNotEmpty.Length > 0)
            {
                Console.WriteLine("\nErrors found in the submitted text file:\n{0}", addressListTextFile);
                Console.WriteLine("List of Errors:\n\n{0}", invalidLinesFoundIfNotEmpty);
                // Write to the same directory, a text file that contains the errors
            }

            // Pause before closing the console' window.
            Console.WriteLine("\n\nPress any key to quit...");
            var anykey = Console.ReadKey();
        }

        /// <summary>
        /// Cleans up and then evaluates a line of text from the user supplied text file.
        /// </summary>
        /// <param name="addressListTextFile"></param>
        /// <param name="holdingDataTable"></param>
        /// <returns></returns>
        private static string ValidateTextFileLine(string addressListTextFile, DataTable holdingDataTable)
        {
            // Load the DataTable with the pseudo-comma delimited data from the user supplied text file
            string[] occupantNameAndAddresses = File.ReadAllLines(addressListTextFile);

            // Get the number of columns
            // This should not be hardcoded, since in the future, more attributes about an 
            // occupant could be added or removed (e.g. Country, Continent, weight, height, etc.).
            int columnCount = holdingDataTable.Columns.Count;
            // Create a string to hold invalid text file lines.
            string badTextLines = "";
            // Create a int to store the numeric value of the occupant.
            int ageOfOccupant = -1;

            foreach (string textLineToEvaluate in occupantNameAndAddresses)
            {
                // Unfortunately, using the comma as a deliminater does not work because
                // any of the string fields could also include a comma that does NOT seerate the field data.
                //
                // Fortunately, there is a pattern that separates data:    ","
                //
                // However, being a text file there could be issues with whitespace between the leading double quote mark
                // and the comma and/or white space between the comma and trailing double quote mark.
                //
                // A regex split with a regular expression (quote mark - white space - comma - white space - quote mark) works well.
                // For future flexibility, the regular expression could be modified to work with other separator symbols such as
                // semicolons, colons, pipes, periods, etc.
                //
                // Create a variable to hold the desired regular expression to split the line.
                var delimiterExpression = "\"\\s*,\\s*\"";

                // Split the text textLineToEvaluate into the various fields.
                var cols = Regex.Split(textLineToEvaluate, delimiterExpression);

                // Validate that the split generated the correct amount of fields to put into the data holding table.
                if (cols.Count() != columnCount)
                {
                    //Add text textLineToEvaluate to a List of Invalid Text Lines
                    badTextLines += "Incorrect Element Count, " + cols.Count() + ":\t" + textLineToEvaluate + "\n";
                    continue;
                };

                // Remove the starting double quotation mark if one exists.
                if ((cols[0].Substring(0, 1) == "\""))
                {
                    cols[0] = cols[0].Replace("\"", " ").Trim();
                };

                // Remove the ending double quotation mark if one exists.
                var lastCharacterIndex = cols[columnCount - 1].Length - 1;
                if (textLineToEvaluate.Substring(textLineToEvaluate.Length - 1) == "\"")
                {
                    cols[columnCount - 1] = cols[columnCount - 1].Replace("\"", " ").Trim();
                };

                // The textLineToEvaluate is invalid if the age is outside the human range of 0 to 125 years. Anything other is a mistake.
                // Note: The longest documented and verified human lifespan is that of Jeanne Calment of France (1875–1997),
                // a woman who lived to age 122 years and 164 days.
                if (!Int32.TryParse(cols[5], out ageOfOccupant))
                {
                    badTextLines += "Age is not fully an integer:\t" + textLineToEvaluate + "\n";
                    continue;
                }
                else
                {
                    if (ageOfOccupant < 0 || ageOfOccupant > 125)
                    {
                        // age is out of range
                        badTextLines += "Age " + ageOfOccupant + " is out of range:\t" + textLineToEvaluate + "\n";
                        continue;
                    }
                }

                // Ensure that Street, City and State are of the same case, otherwise they could
                // be treated as different during sort/group operations.
                DataRow dr = holdingDataTable.NewRow();
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    cols[2] = cols[2].ToUpper().Trim();
                    cols[3] = cols[3].ToUpper().Trim();
                    cols[4] = cols[4].ToUpper().Trim();

                    // For the address field, remove all punctuation to remove not meaningful variations
                    var sb = new StringBuilder();
                    foreach (char c in cols[2])
                    {
                        // Do not add to the stringbuilder if a puctuation/symbol is found.
                        if (!char.IsPunctuation(c) && !char.IsSymbol(c)) sb.Append(c);
                    }
                    cols[2] = sb.ToString();

                    // Add the validated textLineToEvaluate data into the DataT2able
                    dr[columnIndex] = cols[columnIndex];
                };

                // Add the validated data to the DataTable
                holdingDataTable.Rows.Add(dr);
            }

            // Return an empty string if none of the lines of text are invalid.
            return badTextLines;
        }

        /// <summary>
        /// Create a DataTable object with the field’s names defined by the passed string array.
        /// </summary>
        /// <returns>DataTable</returns>
        private static DataTable CreateDataTable(string[] args)
        {
            // Create the DataTable object
            DataTable myDataTable = new DataTable();

            // Loop through the passed string array[] to build out the columns
            var numberOfColumns = args.GetLength(0);
            for (int i = 0; i < numberOfColumns; i++)
            {
                myDataTable.Columns.AddRange(new DataColumn[]
                {
                new DataColumn(args[i],typeof(string))
                });
            }
            return myDataTable;
        }

        /// <summary>
        /// Ask the user for a text file.
        /// Allows the user to exit the program when pressing a 'q' or 'Q'
        /// </summary>
        /// <returns></returns>
        private static string GetTextFileFromUser()
        {
            // As long as invalidFileEntered remains true, the question of the file location will be asked...
            bool invalidFileEntered = true;
            string addressListTextFile = "";

            // As long as invalidFileEntered remains true (the file path and name is invalid or is not found)
            // the user will be continously asked for a file path and name.
            while (invalidFileEntered)
            {
                Console.WriteLine("Please enter a file name and its path to be processed <Enter 'Q' to quit>: ");
                addressListTextFile = Console.ReadLine();

                // Check to see if the user wants to quit
                if (addressListTextFile.Trim().ToLower() == "q")
                {
                    Environment.Exit(-1);
                };

                // Check if a valid txt file and path have been given
                //      Note - For now presume that only a text file of comma separated strings is the only 
                //             format for data input. Other forms are spreadsheets, XML, Database connection,
                //             or other custom formats.

                if (File.Exists(addressListTextFile))
                {
                    Console.WriteLine("\n'{0}' exists and you have read permissions! \n", addressListTextFile);
                    invalidFileEntered = false;
                }

                else
                {
                    // If no valid readable Address List Text File is found the loop continues.
                    Console.WriteLine("\n'{0}' is not a valid text file. Try again!\n", addressListTextFile);
                }
            }

            return addressListTextFile;
        }
    }
}
