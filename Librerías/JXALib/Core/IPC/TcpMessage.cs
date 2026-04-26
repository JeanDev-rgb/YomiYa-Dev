using System.Text.Json;

namespace YomiYa.Core.IPC;

// Esta es la estructura base que viajará por TCP
public class TcpMessage
{
    // El tipo de acción, ej: "GetChapterList", "GetImageHeaders", "Response"
    public string Action { get; set; } 
        
    // Un identificador único para saber a qué petición corresponde esta respuesta
    public string RequestId { get; set; } 
        
    // El contenido del mensaje en formato JSON (puede ser un objeto anidado)
    public string PayloadJson { get; set; } 

    // Método de ayuda para empaquetar un objeto en el Payload
    public void SetPayload<T>(T data)
    {
        PayloadJson = JsonSerializer.Serialize(data);
    }

    // Método de ayuda para desempaquetar el Payload
    public T GetPayload<T>()
    {
        if (string.IsNullOrEmpty(PayloadJson))
            return default;
                
        return JsonSerializer.Deserialize<T>(PayloadJson);
    }
    
    // Aquí definimos la respuesta que solucionará tu problema de las imágenes
    // Esta clase será usada cuando YomiYa pida los datos de una página
    public class ImageRequestResponse
    {
        public string ImageUrl { get; set; }
        public string Referer { get; set; }
        // Puedes agregar más encabezados si otras páginas lo requieren a futuro
        public string UserAgent { get; set; } 
    }
}