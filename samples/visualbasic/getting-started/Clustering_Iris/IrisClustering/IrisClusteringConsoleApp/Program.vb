﻿Imports System.IO

Imports Microsoft.ML
Imports Common
Imports Clustering_Iris.DataStructures
Imports Microsoft.ML.Core.Data
Imports Microsoft.ML.Data
Imports Microsoft.Data.DataView

Namespace Clustering_Iris
    Friend Module Program
        Private ReadOnly Property AppPath As String
            Get
                Return Path.GetDirectoryName(Environment.GetCommandLineArgs()(0))
            End Get
        End Property

        Private BaseDatasetsRelativePath As String = "../../../../Data"
        Private DataSetRealtivePath As String = $"{BaseDatasetsRelativePath}/iris-full.txt"

        Private DataPath As String = GetAbsolutePath(DataSetRealtivePath)

        Private BaseModelsRelativePath As String = "../../../../MLModels"
        Private ModelRelativePath As String = $"{BaseModelsRelativePath}/IrisModel.zip"

        Private ModelPath As String = GetAbsolutePath(ModelRelativePath)

        Public Sub Main(args() As String)
            'Create the MLContext to share across components for deterministic results
            Dim mlContext As New MLContext(seed:=1) 'Seed set to any number so you have a deterministic environment

            ' STEP 1: Common data loading configuration            
            Dim fullData As IDataView = mlContext.Data.ReadFromTextFile(path:=DataPath, columns:={
                New TextLoader.Column(DefaultColumnNames.Label, DataKind.R4, 0),
                New TextLoader.Column(NameOf(IrisData.SepalLength), DataKind.R4, 1),
                New TextLoader.Column(NameOf(IrisData.SepalWidth), DataKind.R4, 2),
                New TextLoader.Column(NameOf(IrisData.PetalLength), DataKind.R4, 3),
                New TextLoader.Column(NameOf(IrisData.PetalWidth), DataKind.R4, 4)
            }, hasHeader:=True, separatorChar:=vbTab)

            'Split dataset in two parts: TrainingDataset (80%) and TestDataset (20%)
            Dim trainingDataView, testingDataView As IDataView

            With mlContext.Clustering.TrainTestSplit(fullData, testFraction:=0.2)
                trainingDataView = .trainSet
                testingDataView = .testSet
            End With

            'STEP 2: Process data transformations in pipeline
            Dim dataProcessPipeline = mlContext.Transforms.Concatenate(DefaultColumnNames.Features, NameOf(IrisData.SepalLength), NameOf(IrisData.SepalWidth), NameOf(IrisData.PetalLength), NameOf(IrisData.PetalWidth))

            ' (Optional) Peek data in training DataView after applying the ProcessPipeline's transformations  
            Common.ConsoleHelper.PeekDataViewInConsole(mlContext, trainingDataView, dataProcessPipeline, 10)
            Common.ConsoleHelper.PeekVectorColumnDataInConsole(mlContext, DefaultColumnNames.Features, trainingDataView, dataProcessPipeline, 10)

            ' STEP 3: Create and train the model     
            Dim trainer = mlContext.Clustering.Trainers.KMeans(featureColumn:=DefaultColumnNames.Features, clustersCount:=3)
            Dim trainingPipeline = dataProcessPipeline.Append(trainer)
            Dim trainedModel = trainingPipeline.Fit(trainingDataView)

            ' STEP4: Evaluate accuracy of the model
            Dim predictions As IDataView = trainedModel.Transform(testingDataView)
            Dim metrics = mlContext.Clustering.Evaluate(predictions, score:=DefaultColumnNames.Score, features:=DefaultColumnNames.Features)

            ConsoleHelper.PrintClusteringMetrics(trainer.ToString(), metrics)

            ' STEP5: Save/persist the model as a .ZIP file
            Using fs = New FileStream(ModelPath, FileMode.Create, FileAccess.Write, FileShare.Write)
                mlContext.Model.Save(trainedModel, fs)
            End Using

            Console.WriteLine("=============== End of training process ===============")

            Console.WriteLine("=============== Predict a cluster for a single case (Single Iris data sample) ===============")

            ' Test with one sample text 
            Dim sampleIrisData = New IrisData With {
                .SepalLength = 3.3F,
                .SepalWidth = 1.6F,
                .PetalLength = 0.2F,
                .PetalWidth = 5.1F
            }

            Using stream = New FileStream(ModelPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Dim model As ITransformer = mlContext.Model.Load(stream)
                ' Create prediction engine related to the loaded trained model
                Dim predEngine = model.CreatePredictionEngine(Of IrisData, IrisPrediction)(mlContext)

                'Score
                Dim resultprediction = predEngine.Predict(sampleIrisData)

                Console.WriteLine($"Cluster assigned for setosa flowers:" & resultprediction.SelectedClusterId)
            End Using

            Console.WriteLine("=============== End of process, hit any key to finish ===============")
            Console.ReadKey()
        End Sub

        Public Function GetAbsolutePath(relativePath As String) As String
            Dim _dataRoot As New FileInfo(GetType(Program).Assembly.Location)
            Dim assemblyFolderPath As String = _dataRoot.Directory.FullName

            Dim fullPath As String = Path.Combine(assemblyFolderPath, relativePath)

            Return fullPath
        End Function
    End Module

End Namespace
