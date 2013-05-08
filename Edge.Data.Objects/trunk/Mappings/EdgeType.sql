-- # -------------------------------------
-- # TEMPLATE Get

select
	TypeID,
	BaseTypeID,
	ClrType,
	Name,
	IsAbstract,
	TableName,
	AccountID,
	ChannelID
from MD_EdgeType
where
	AccountID in (-1, @accountID) and
	ChannelID in (-1, @channelID)
;

-- # -------------------------------------
-- # TEMPLATE Get/EdgeTypeFields

select
	types.TypeID as ParentTypeID,
	fields.FieldID as FieldID,
	fields.FieldType as FieldType,
	fields.FieldTypeID as FieldTypeID,
	fields.Name as Name,
	typeFields.ColumnName as ColumnName,
	typeFields.IsIdentity as IsIdentity
from
	MD_EdgeType as types
	inner join MD_EdgeTypeField as typeFields on
		typeFields.ParentTypeID in (-1, types.TypeID)
	inner join MD_EdgeField as fields on
		fields.FieldID = typeFields.FieldID and
		fields.AccountID in (-1, types.AccountID) and
		fields.ChannelID in (-1, types.ChannelID)

where
	types.AccountID in (-1, @accountID) and
	types.ChannelID in (-1, @channelID)
;