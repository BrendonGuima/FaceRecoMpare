using Microsoft.AspNetCore.Mvc;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;

namespace FaceApiRecognizeTest.Controllers
{
    public class FaceRecognitionApiController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly AmazonRekognitionClient _rekognitionClient;

        public FaceRecognitionApiController(IWebHostEnvironment environment)
        {
            _environment = environment;
            _rekognitionClient = new AmazonRekognitionClient("awsAccessKeyId",
                "awsSecretAcessKey", Amazon.RegionEndpoint.USEast2);
        }

        [HttpPost("compare")]
        public async Task<ActionResult> CompareFaces(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return BadRequest("No image URL provided.");
            }

            using (var httpClient = new HttpClient())
            using (var stream = await httpClient.GetStreamAsync(imageUrl))
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                var buffer = ms.ToArray();
                var request = new DetectFacesRequest
                {
                    Image = new Amazon.Rekognition.Model.Image
                    {
                        Bytes = new MemoryStream(buffer)
                    },
                    Attributes = new List<string> { "ALL" } // ou "DEFAULT" dependendo do que você precisa
                };
                var detectedFaces = await _rekognitionClient.DetectFacesAsync(request);
                if (detectedFaces.FaceDetails.Count == 0)
                {
                    return NotFound("No face detected in the uploaded image.");
                }

                var dbImagesPath = Path.Combine(_environment.WebRootPath, "Images");
                var dbImageFiles = Directory.GetFiles(dbImagesPath, "*.jpg");

                var matchingImages = new List<string>();

                foreach (var dbImageFile in dbImageFiles)
                {
                    using (var dbImageStream = new FileStream(dbImageFile, FileMode.Open))
                    {
                        var imageBytes = new byte[dbImageStream.Length];
                        await dbImageStream.ReadAsync(imageBytes, 0, (int)dbImageStream.Length);
                        var dbDetectFacesRequest = new DetectFacesRequest
                        {
                            Image = new Image
                            {
                                Bytes = new MemoryStream(imageBytes)
                            },
                            Attributes = new List<string> { "ALL" } // ou "DEFAULT" dependendo do que você precisa
                        };
                        var dbDetectedFaces = await _rekognitionClient.DetectFacesAsync(dbDetectFacesRequest);
                        if (dbDetectedFaces.FaceDetails.Count > 0)
                        {
                            foreach (var detectedFace in detectedFaces.FaceDetails)
                            {
                                foreach (var dbDetectedFace in dbDetectedFaces.FaceDetails)
                                {
                                    var similarity = await _rekognitionClient.CompareFacesAsync(new CompareFacesRequest
                                    {
                                        SourceImage = new Image
                                        {
                                            Bytes = new MemoryStream(buffer)
                                        },
                                        TargetImage = new Image
                                        {
                                            Bytes = new MemoryStream(imageBytes)
                                        }
                                    });
                                    if (similarity.FaceMatches.Count > 0 && similarity.FaceMatches[0].Similarity > 0.5)
                                    {
                                        matchingImages.Add(Path.GetFileName(dbImageFile));
                                        break; // Sai do loop interno, pois já encontrou uma correspondência
                                    }
                                }
                            }
                        }
                    }
                }

                if (matchingImages.Count > 0)
                {
                    return Ok(matchingImages);
                }
            }

            return NotFound("No matching images found in database images.");
        }
    }
}