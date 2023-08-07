//! <----- sam local start-lambda --host 0.0.0.0 ----->
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using System;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{
  public class Function
  {
    private static string[] FILES_TO_MERGE = new string[0];
    private static string FILENAME;
    private static string ACCESS_KEY = "Q3AM3UQ867SPQQA43P2F";
    private static string SECRET_KEY = "zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG";
    private static string ENDPOINT = "https://play.min.io:9000";
    private static string BUCKET_NAME;
    private static AmazonS3Config s3Config = new AmazonS3Config
    {
      ServiceURL = ENDPOINT,
      ForcePathStyle = true
    };
    private static AmazonS3Client s3Client = new AmazonS3Client(
        ACCESS_KEY,
        SECRET_KEY,
        s3Config
    );

    public class DataToSend
    {
      public string[] args { get; set; }
    }
    public async Task<APIGatewayProxyResponse> FunctionHandler(JsonElement apigProxyEvent, ILambdaContext context)

    {
      DataToSend data = JsonSerializer.Deserialize<DataToSend>(apigProxyEvent.GetRawText());
      string[] args = data.args;

      var body = new Dictionary<string, string[]>
            {
                { "args", args },
            };

      foreach (string item in args)
      {
        if (item.Contains("--files"))
        {
          string filesString = item.Split("=")[1];
          FILES_TO_MERGE = filesString.Split(',');
        }
        if (item.Contains("--bucket"))
        {
          string bucket = item.Split("=")[1];
          BUCKET_NAME = bucket;
        }
        if (item.Contains("--filename"))
        {
          string filename = item.Split("=")[1];
          FILENAME = filename;
        }
      }

      if (String.IsNullOrEmpty(BUCKET_NAME) || FILES_TO_MERGE?.Length < 1 || String.IsNullOrEmpty(FILENAME) || args.Length < 3)
      {
        Console.WriteLine("Merge PDF failed. Please provide all of required arguments.");
        Console.WriteLine("Example usage: --files=file1.pdf,file2.pdf --bucket=00bucket --filename=/outputFilename.pdf");
        Environment.Exit(1);
      }

      PdfWriter writer = new PdfWriter(FILENAME);
      writer.SetSmartMode(true);
      PdfDocument pdfDocument = new PdfDocument(writer);
      PdfMerger merger = new PdfMerger(pdfDocument);

      var i = 1;
      foreach (string item in FILES_TO_MERGE)
      {
        Console.WriteLine("Merging: " + item + " (" + i + " of " + FILES_TO_MERGE.Length + ")");

        //! get documents from bucket
        GetObjectResponse getResponse = await s3Client.GetObjectAsync(BUCKET_NAME, item);
        Console.WriteLine("Files Downloaded");

        MemoryStream memoryStream = new MemoryStream();

        await getResponse.ResponseStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        //! copy documents to memory stream
        PdfDocument mergingDocument = new PdfDocument(new PdfReader(memoryStream));
        merger.Merge(mergingDocument, 1, mergingDocument.GetNumberOfPages());
        mergingDocument.Close();
        i++;
      }

      pdfDocument.Close();
      Console.WriteLine("Merging pdf done, created " + FILENAME);

      string objectName = FILENAME;
      string filePath = FILENAME;

      PutObjectRequest request = new PutObjectRequest();
      request.BucketName = BUCKET_NAME;
      request.Key = objectName;
      request.FilePath = filePath;

      await s3Client.PutObjectAsync(request);
      Console.WriteLine("Successfully uploaded " + objectName);


      //! delete files from bucket after merging
      File.Delete(FILENAME);

      foreach (string item in FILES_TO_MERGE)
      {
        DeleteObjectRequest delRequest = new DeleteObjectRequest
        {
          BucketName = BUCKET_NAME,
          Key = item,
        };

        await s3Client.DeleteObjectAsync(delRequest);
        Console.WriteLine("File: " + item + " deleted successfully");
      }

      return new APIGatewayProxyResponse
      {
        Body = JsonSerializer.Serialize(body),
        StatusCode = 200,
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
      };
    }
  }
}