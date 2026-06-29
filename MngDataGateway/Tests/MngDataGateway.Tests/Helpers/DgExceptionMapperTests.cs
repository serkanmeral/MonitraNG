using MngDataGateway.Domain.Constants;
using MngDataGateway.Persistence.Helpers;
using Xunit;

namespace MngDataGateway.Tests.Helpers;

public class DgExceptionMapperTests
{
    [Fact]
    public void ExtractFieldFromMongoMessage_ParsesDupKeyField()
    {
        var field = DgExceptionMapper.ExtractFieldFromMongoMessage(
            "E11000 duplicate key error collection: monitra_odak_com.odak_musteriler index: code_1 dup key: { code: \"X1\" }");

        Assert.Equal("code", field);
    }

    [Fact]
    public void Map_ArgumentException_ReturnsBadRequest()
    {
        var mapped = DgExceptionMapper.Map(new ArgumentException("bad input"), "ctx");

        Assert.IsType<Domain.Exceptions.BadRequestException>(mapped);
        Assert.Equal(ErrorCodes.INVALID_ARGUMENT, mapped.ErrorCode);
    }
}
