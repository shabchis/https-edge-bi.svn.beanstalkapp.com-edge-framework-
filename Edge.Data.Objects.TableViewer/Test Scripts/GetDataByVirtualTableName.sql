DECLARE	@return_value int

EXEC	@return_value = [dbo].[GetDataByVirtualTableName]
		@accountID = -1,
		@virtualTableName = N'Color',
		@deliveryOutputID = NULL,
		@dateCreated = NULL

--SELECT	'Return Value' = @return_value
