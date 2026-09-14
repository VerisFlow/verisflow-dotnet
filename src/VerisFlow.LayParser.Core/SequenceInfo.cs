using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;

namespace VerisFlow.LayParser.Core
{
    /// <summary>
    /// Represents a specific well or slot position within a sequence matrix.
    /// </summary>
    public class SequenceWellPosition
    {
        /// <summary>
        /// Gets or sets the 1-based sequential order index in the overall sequence.
        /// </summary>
        public int SequenceIndex { get; set; }

        /// <summary>
        /// Gets or sets the position identifier (e.g., "A1", "H12", or numeric index).
        /// </summary>
        public string PosId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the 1-based row index calculated from PosId.
        /// </summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// Gets or sets the 1-based column index calculated from PosId.
        /// </summary>
        public int ColumnIndex { get; set; }

        public override string ToString()
        {
            return FormattableString.Invariant($"[{SequenceIndex}] {PosId} (R{RowIndex}, C{ColumnIndex})");
        }
    }

    /// <summary>
    /// Represents a grouped well matrix mapped to a specific labware rack or container.
    /// </summary>
    public class SequenceRackMatrix
    {
        /// <summary>
        /// Gets or sets the target labware object identifier.
        /// </summary>
        public string ObjId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of wells assigned to this rack matrix.
        /// </summary>
        public int TotalWells { get; set; }

        /// <summary>
        /// Gets or sets the ordered list of well positions belonging to this rack matrix.
        /// </summary>
        public List<SequenceWellPosition> Positions { get; set; } = new List<SequenceWellPosition>();

        public override string ToString()
        {
            return FormattableString.Invariant($"ObjId={ObjId}, Wells={Positions.Count}");
        }
    }

    /// <summary>
    /// Represents complete sequence definition extracted from a deck layout file.
    /// </summary>
    public class SequenceInfo
    {
        /// <summary>
        /// Gets or sets the 1-based index of the sequence definition in the layout file.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the unique name of the sequence.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total declared point count of the sequence.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the sequence is read-only.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the rack matrices grouped and ordered descending by ObjId.
        /// </summary>
        public List<SequenceRackMatrix> Matrices { get; set; } = new List<SequenceRackMatrix>();

        public override string ToString()
        {
            return FormattableString.Invariant($"Seq {Index} [{Name}]: Cnt={TotalCount}, Racks={Matrices.Count}, ReadOnly={ReadOnly}");
        }
    }
}