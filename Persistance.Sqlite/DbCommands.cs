using System;
using System.Collections.Generic;
using System.Text;

namespace Persistance.Sqlite
{
    enum Operation
    {
        GetSlot,
        GetItem,
        GetItemWithDate,
        InsertSlot,
        InsertItem,
        UpdateItemCount,
        GetMonitorNames,
        CreateDb
    }

    static class DbCommands
    {
        public static readonly string[] Commands;
        public static readonly int Count = Enum.GetValues(typeof(Operation)).Length;

        static DbCommands()
        {
            Commands = new string[Count];

            Commands[(int)Operation.GetSlot] =
                "SELECT Id FROM Slots WHERE Name=@name AND Scale=@scale";

            Commands[(int)Operation.GetItem] =
                "SELECT MAX(Time) as Time, Count, Id FROM Items WHERE SlotId=@slot";

            Commands[(int)Operation.GetItemWithDate] =
                "SELECT Count, Time FROM Items WHERE SlotId=@slot AND Time>=@from AND Time <= @to";

            Commands[(int)Operation.InsertSlot] =
                "INSERT INTO Slots (Name, Duration, Created, Scale) " +
                "VALUES (@name, @duration, @created, @scale)";

            Commands[(int)Operation.InsertItem] =
                "INSERT INTO Items (Count, Time, SlotId) VALUES ( @count, @time, @slotid)";
                

            Commands[(int)Operation.UpdateItemCount] =
                "UPDATE Items SET Count = @count WHERE Id = @id";

            Commands[(int)Operation.GetMonitorNames] =
                "SELECT DISTINCT Name FROM Slots";
            
            Commands[(int)Operation.CreateDb] =
                "CREATE TABLE \"Items\" ( \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Items\" PRIMARY KEY AUTOINCREMENT, \"Count\" INTEGER NOT NULL, \"Information\" TEXT NULL,  \"Time\" TEXT NOT NULL, \"SlotId\" INTEGER NOT NULL, CONSTRAINT \"FK_Items_Slots_SlotId\" FOREIGN KEY (\"SlotId\") REFERENCES \"Slots\" (\"Id\") ON DELETE CASCADE);" +
                "CREATE TABLE \"Slots\" ( \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Slots\" PRIMARY KEY AUTOINCREMENT, \"Name\" TEXT NOT NULL, \"Duration\" INTEGER NOT NULL,  \"Created\" TEXT NOT NULL, \"Scale\" INTEGER NOT NULL);" +
                "CREATE TABLE \"Meta\" ( \"Key\"\tTEXT NOT NULL, \"Value\"\tINTEGER, PRIMARY KEY(\"Key\")) WITHOUT ROWID;" +
                "CREATE INDEX \"IX_Items_SlotId\" ON \"Items\" (\"SlotId\");" +
                "CREATE INDEX \"IX_Items_Time\" ON \"Items\" (\"Time\");" +
                "CREATE INDEX \"IX_Slots_Name\" ON \"Slots\" (\"Name\");"+
                "CREATE INDEX \"IX_Slots_Scale\" ON \"Slots\" (\"Scale\");";

        }
    }
}
