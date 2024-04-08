using FaceApiRecognizeTest.Controllers;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace FaceApiRecognizeTest.Services;

public class FaceRecognitionService
{
    private readonly IFaceClient _faceClient;
    
    public FaceRecognitionService()
    {
        _faceClient = new FaceClient(new ApiKeyServiceClientCredentials(Constants.FaceApiKey))
        {
            Endpoint = Constants.FaceApiEndpoint
        };
    }
    public async Task<DetectedFace[]> DetectFacesAsync(string imageUrl)
    {
        // Detectar rostos na imagem
        try
        {
            IList<DetectedFace> detectedFaces = await _faceClient.Face.DetectWithUrlAsync(imageUrl);
            return detectedFaces.ToArray();
        }
        catch (APIErrorException ex)
        {
            // Tratar erros de API
            Console.WriteLine($"Erro na detecção de rostos: {ex.Message}");
            return new DetectedFace[0];
        }
    }
}