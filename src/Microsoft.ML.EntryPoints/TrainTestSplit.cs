// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.DataView;
using Microsoft.ML;
using Microsoft.ML.CommandLine;
using Microsoft.ML.Data;
using Microsoft.ML.EntryPoints;
using Microsoft.ML.Runtime;
using Microsoft.ML.Transforms;

[assembly: LoadableClass(typeof(void), typeof(TrainTestSplit), null, typeof(SignatureEntryPointModule), "TrainTestSplit")]

namespace Microsoft.ML.EntryPoints
{
    internal static class TrainTestSplit
    {
        public sealed class Input
        {
            [Argument(ArgumentType.Required, HelpText = "Input dataset", SortOrder = 1)]
            public IDataView Data;

            [Argument(ArgumentType.AtMostOnce, HelpText = "Fraction of training data", SortOrder = 2)]
            public float Fraction = 0.8f;

            [Argument(ArgumentType.AtMostOnce, ShortName = "strat", HelpText = "Stratification column", SortOrder = 3)]
            public string StratificationColumn = null;
        }

        public sealed class Output
        {
            [TlcModule.Output(Desc = "Training data", SortOrder = 1)]
            public IDataView TrainData;

            [TlcModule.Output(Desc = "Testing data", SortOrder = 2)]
            public IDataView TestData;
        }

        public const string ModuleName = "TrainTestSplit";
        public const string UserName = "Dataset Train-Test Split";

        [TlcModule.EntryPoint(Name = "Transforms.TrainTestDatasetSplitter", Desc = "Split the dataset into train and test sets", UserName = UserName)]
        public static Output Split(IHostEnvironment env, Input input)
        {
            Contracts.CheckValue(env, nameof(env));
            var host = env.Register(ModuleName);
            host.CheckValue(input, nameof(input));
            host.Check(0 < input.Fraction && input.Fraction < 1, "The fraction must be in the interval (0,1).");

            EntryPointUtils.CheckInputArgs(host, input);

            var data = input.Data;
            var stratCol = SplitUtils.CreateStratificationColumn(host, ref data, input.StratificationColumn);

            IDataView trainData = new RangeFilter(host,
                new RangeFilter.Options { Column = stratCol, Min = 0, Max = input.Fraction, Complement = false }, data);
            trainData = ColumnSelectingTransformer.CreateDrop(host, trainData, stratCol);

            IDataView testData = new RangeFilter(host,
                new RangeFilter.Options { Column = stratCol, Min = 0, Max = input.Fraction, Complement = true }, data);
            testData = ColumnSelectingTransformer.CreateDrop(host, testData, stratCol);

            return new Output() { TrainData = trainData, TestData = testData };
        }

    }

    internal static class SplitUtils
    {
        public static string CreateStratificationColumn(IHost host, ref IDataView data, string stratificationColumn = null)
        {
            host.CheckValue(data, nameof(data));
            host.CheckValueOrNull(stratificationColumn);

            // Pick a unique name for the stratificationColumn.
            const string stratColName = "StratificationKey";
            string stratCol = stratColName;
            int col;
            int j = 0;
            while (data.Schema.TryGetColumnIndex(stratCol, out col))
                stratCol = string.Format("{0}_{1:000}", stratColName, j++);
            // Construct the stratification column. If user-provided stratification column exists, use HashJoin
            // of it to construct the strat column, otherwise generate a random number and use it.
            if (stratificationColumn == null)
            {
                data = new GenerateNumberTransform(host,
                    new GenerateNumberTransform.Options
                    {
                        Columns = new[] { new GenerateNumberTransform.Column { Name = stratCol } }
                    }, data);
            }
            else
            {
                data = new HashJoiningTransform(host,
                    new HashJoiningTransform.Arguments
                    {
                        Columns = new[] { new HashJoiningTransform.Column { Name = stratCol, Source = stratificationColumn } },
                        Join = true,
                        NumberOfBits = 30
                    }, data);
            }

            return stratCol;
        }
    }
}