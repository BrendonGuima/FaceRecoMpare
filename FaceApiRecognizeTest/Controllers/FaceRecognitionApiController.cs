using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Mvc;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace FaceApiRecognizeTest.Controllers
{
    public class FaceRecognitionApiController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly AmazonRekognitionClient _rekognitionClient;
        private const float SimilarityThreshold = 70F;
        private const int TargetImageWidth = 256;
        private const int TargetImageHeight = 256;

        public FaceRecognitionApiController(IWebHostEnvironment environment)
        {
            _environment = environment;
            _rekognitionClient = new AmazonRekognitionClient("awsAccessKeyId",
                "awsSecretAcessKey", Amazon.RegionEndpoint.USEast2);
        }

        [HttpPost("compare")]
        public async Task<IActionResult> CompareFaces(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return BadRequest("No image URL provided.");
            }

            Amazon.Rekognition.Model.Image imageSource = new Amazon.Rekognition.Model.Image();
            using (var httpClient = new HttpClient())
            await using (var stream = await httpClient.GetStreamAsync(imageUrl))
            await using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                var buffer = ms.ToArray();
                imageSource.Bytes = new MemoryStream(buffer);
                if (imageSource.Bytes == null)
                {
                    return NotFound("No face detected in the uploaded image.");
                }

                var dbImagesPath = Path.Combine(_environment.WebRootPath, "Images");
                var dbImageFiles = Directory.GetFiles(dbImagesPath, "*.jpg");

                var matchingImages = new ConcurrentBag<string>();

                var options = new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                var block = new ActionBlock<string>(async dbImageFile =>
                {
                    await using (var dbImageStream = new FileStream(dbImageFile, FileMode.Open))
                    {
                        using (var image = SixLabors.ImageSharp.Image.Load(dbImageStream))
                        {
                            image.Mutate(x => x.Resize(TargetImageWidth, TargetImageHeight));
                            using (var resizedImageStream = new MemoryStream())
                            {
                                image.Save(resizedImageStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                                resizedImageStream.Seek(0, SeekOrigin.Begin);

                                Amazon.Rekognition.Model.Image targetSource = new Amazon.Rekognition.Model.Image();
                                targetSource.Bytes = resizedImageStream;

                                CompareFacesRequest compareFacesRequest = new CompareFacesRequest()
                                {
                                    SourceImage = imageSource,
                                    TargetImage = targetSource,
                                    SimilarityThreshold = SimilarityThreshold
                                };
                                CompareFacesResponse compareFacesResponse = await _rekognitionClient.CompareFacesAsync(compareFacesRequest);
                                foreach (var detectedFace in compareFacesResponse.FaceMatches)
                                {
                                    if (detectedFace.Similarity > SimilarityThreshold)
                                    {
                                        matchingImages.Add(Path.GetFileName(dbImageFile));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }, options);

                foreach (var dbImageFile in dbImageFiles)
                {
                    await block.SendAsync(dbImageFile);
                }

                block.Complete();
                await block.Completion;

                if (matchingImages.Count > 0)
                {
                    return Ok(matchingImages.ToList());
                }
            }

            return NotFound("No matching images found in database images.");
        }
    }
}
