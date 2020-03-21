// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    // Formats table data for display with a monospaced font.
    // Normal usage is to subclass this formatter to create a formatter
    // that is specific to a particular data set.
    abstract class MonospaceTableFormatter
    {
        protected static IEnumerable<string> GetTableLines(
            ColumnData[][] headers,
            ColumnData[][] rows)
        {
            if (rows.Length == 0)
            {
                yield break;
            }

            // Get the width of each column. If a column spans more than one
            // column, add 1 extra to account for the space that would otherwise
            // be taken up by each column separator.
            var columnWidths =
                (from row in headers.Concat(rows)
                 where row != null
                 select (from col in row
                         let width = col.Text.Length + col.Span + 1
                         select width).ToArray()
                 ).Aggregate((w1, w2) => w1.Select((w, i) => Math.Max(w2[i], w)).ToArray()).ToArray();

            // The total width includes space for the column separators.
            var totalWidth = columnWidths.Sum() + columnWidths.Length - 1;

            // Output a line at the top of the table.
            yield return new string('_', totalWidth + 2);

            // Output the headers.
            foreach (var header in headers)
            {
                yield return FormatRow(header.Select((x, i) => (x, columnWidths[i])));
            }

            // Output a ruler line between the headers and the rows.
            yield return $"|{string.Join("|", columnWidths.Select(w => new string('_', w)))}|";

            foreach (var r in rows)
            {
                if (r is null)
                {
                    // A null is a request to insert a row separator.
                    yield return $"|{string.Join('+', columnWidths.Select(w => new string('-', w)))}|";
                }
                else
                {
                    yield return FormatRow(r.Select((x, i) => (x, columnWidths[i])));
                }
            }

            // Output a line at the bottom of the table.
            yield return new string('-', totalWidth + 2);
        }

        static string Align(string str, int width, TextAlignment alignment)
        {
            var padding = width - str.Length;
            switch (alignment)
            {
                case TextAlignment.Center:
                    {
                        if (padding == 0)
                        {
                            return str;
                        }

                        var leftPadding = padding / 2;
                        if (leftPadding == 0)
                        {
                            leftPadding = 1;
                        }

                        return new string(' ', leftPadding) + str + new string(' ', padding - leftPadding);
                    }

                case TextAlignment.Left:
                    return padding == 0
                        ? str
                        : ' ' + str + new string(' ', padding - 1);

                case TextAlignment.Right:
                    return padding == 0
                        ? str
                        : new string(' ', padding - 1) + str + " ";

                default:
                    throw new InvalidOperationException();
            }
        }

        static string FormatRow(IEnumerable<(ColumnData data, int width)> rowData)
        {
            var sb = new StringBuilder();

            var spanWidth = 0;
            string spanText = null;
            TextAlignment spanAlignment = default(TextAlignment);
            int spanCountdown = -1;

            foreach (var (column, width) in rowData)
            {
                if (spanCountdown == 0)
                {
                    // Output the spanning column.
                    sb.Append("|");
                    sb.Append(Align(spanText, spanWidth, spanAlignment));
                    spanCountdown = -1;
                }
                else if (spanCountdown > 0)
                {
                    // Accumulate the width and otherwise ignore this column.
                    spanWidth += width + 1;
                    spanCountdown--;
                    continue;
                }

                if (column.Span == 1)
                {
                    // Output the column.
                    sb.Append("|");
                    sb.Append(Align(column.Text, width, column.Alignment));
                }
                else
                {
                    // Span is > 1.
                    // Save the column information until the column has been spanned.
                    spanCountdown = column.Span - 1;
                    spanText = column.Text;
                    spanAlignment = column.Alignment;
                    spanWidth = width;
                }
            }

            // Output the final column.
            if (spanCountdown == 0)
            {
                // Output the previous column.
                sb.Append("|");
                sb.Append(Align(spanText, spanWidth, spanAlignment));
            }

            sb.Append("|");

            return sb.ToString();
        }
    }
}