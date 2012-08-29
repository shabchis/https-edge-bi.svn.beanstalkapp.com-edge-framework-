--DECLARE	@return_value int

--EXEC	@return_value = [dbo].[GetDataByAccountID]
--		@accountID = Null,
--		@virtualTableName = N'Color',
--		@deliveryOutputID = NULL,
--		@dateCreated = NULL

--SELECT	'Return Value' = @return_value


--DECLARE	@return_value int

--EXEC	@return_value = [dbo].[GetDataByAccountID]
--		@accountID = 10035,
--		@virtualTableName = N'Color',
--		@deliveryOutputID = NULL,
--		@dateCreated = NULL

--SELECT	'Return Value' = @return_value


DECLARE	@return_value int

EXEC	@return_value = [dbo].[GetTablesNamesByAccountID]
		@accountID = 95

SELECT	'Return Value' = @return_value