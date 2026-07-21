using MngLLM.Application.DTOs.Di;

namespace MngLLM.Application.Services;

public interface IUblEarsivFaturaMapper
{
    EarsivFaturaExtractDto Map(byte[] xmlBytes, string? resourceId = null);
}
