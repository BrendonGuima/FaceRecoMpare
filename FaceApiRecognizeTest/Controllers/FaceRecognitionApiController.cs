using Microsoft.AspNetCore.Mvc;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;

namespace FaceApiRecognizeTest.Controllers
{
    public class FaceRecognitionApiController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly AmazonRekognitionClient _rekognitionClient;
        private const float similarityThreshold = 70F;
        

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

            Amazon.Rekognition.Model.Image imageSource = new Amazon.Rekognition.Model.Image(); // Cria uma instância de Image para imagem recebida
            using (var httpClient = new HttpClient())
            using (var stream = await httpClient.GetStreamAsync(imageUrl))
            using (var ms = new MemoryStream())
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

                var matchingImages = new List<string>();

                foreach (var dbImageFile in dbImageFiles)
                {
                    using (var dbImageStream = new FileStream(dbImageFile, FileMode.Open))
                    {
                        Amazon.Rekognition.Model.Image targetSource = new Amazon.Rekognition.Model.Image(); // Cria uma instância de Image para imagem da db
                        var imageBytes = new byte[dbImageStream.Length];
                        await dbImageStream.ReadAsync(imageBytes, 0, (int)dbImageStream.Length);
                        targetSource.Bytes = new MemoryStream(imageBytes);
                        
                        
                        if (targetSource.Bytes.Length > 0)
                        {
                            CompareFacesRequest compareFacesRequest = new CompareFacesRequest()
                            {
                                SourceImage = imageSource,
                                TargetImage = targetSource,
                                SimilarityThreshold = similarityThreshold
                            };
                            CompareFacesResponse compareFacesResponse = _rekognitionClient.CompareFacesAsync(compareFacesRequest).Result;
                            foreach (var detectedFace in compareFacesResponse.FaceMatches)
                            {
                                if (detectedFace.Similarity > similarityThreshold)
                                {
                                    matchingImages.Add(Path.GetFileName(dbImageFile));
                                    break; // Sai do loop interno, pois já encontrou uma correspondência
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