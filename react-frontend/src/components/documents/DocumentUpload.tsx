import { useRef } from 'react'
import { Upload, FileText, Loader2, AlertCircle, CheckCircle2, Trash2 } from 'lucide-react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { listDocuments, uploadDocument, deleteDocument } from '@/api/client'
import type { UploadResult } from '@/api/client'
import { cn } from '@/lib/utils'

export function DocumentUpload() {
  const fileRef = useRef<HTMLInputElement>(null)
  const queryClient = useQueryClient()

  const { data: docs = [], isError } = useQuery({
    queryKey: ['documents'],
    queryFn: listDocuments,
    retry: 2,
  })

  const upload = useMutation<UploadResult, Error, File>({
    mutationFn: (file: File) => uploadDocument(file),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['documents'] }),
  })

  const remove = useMutation({
    mutationFn: (name: string) => deleteDocument(name),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['documents'] }),
  })

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) upload.mutate(file)
    if (fileRef.current) fileRef.current.value = ''
  }

  const isDuplicate = upload.isError && upload.error?.message?.toLowerCase().includes('already indexed')

  return (
    <div className="p-3 space-y-3">
      <h3 className="text-xs font-semibold uppercase tracking-wider text-gray-500 px-1">
        Documents
      </h3>

      <button
        onClick={() => fileRef.current?.click()}
        disabled={upload.isPending}
        className={cn(
          'w-full flex items-center gap-2 rounded-lg border border-dashed px-3 py-2.5',
          'text-xs transition-colors',
          upload.isPending
            ? 'border-[#3e3e3e] text-gray-500 cursor-not-allowed'
            : 'border-[#3e3e3e] text-gray-500 hover:border-violet-500 hover:text-violet-400 cursor-pointer'
        )}
      >
        {upload.isPending ? (
          <Loader2 size={13} className="animate-spin" />
        ) : (
          <Upload size={13} />
        )}
        {upload.isPending ? 'Indexing...' : 'Upload document (.txt .md .pdf)'}
      </button>

      {upload.isError && (
        <div className="flex items-start gap-1.5 text-xs px-1">
          <AlertCircle size={12} className={cn('flex-shrink-0 mt-0.5', isDuplicate ? 'text-amber-400' : 'text-red-400')} />
          <span className={isDuplicate ? 'text-amber-400' : 'text-red-400'}>
            {isDuplicate
              ? 'Already indexed. Delete it first to re-upload.'
              : (upload.error instanceof Error ? upload.error.message : 'Upload failed')}
          </span>
        </div>
      )}

      {upload.isSuccess && (
        <div className="flex items-center gap-1.5 text-xs text-green-400 px-1">
          <CheckCircle2 size={12} />
          Indexed {upload.data.chunkCount} chunk{upload.data.chunkCount !== 1 ? 's' : ''}
        </div>
      )}

      {remove.isError && (
        <div className="flex items-center gap-1.5 text-xs text-red-400 px-1">
          <AlertCircle size={12} />
          {remove.error instanceof Error ? remove.error.message : 'Delete failed'}
        </div>
      )}

      <input
        ref={fileRef}
        type="file"
        accept=".txt,.md,.csv,.pdf"
        onChange={handleFileChange}
        className="hidden"
        aria-label="Choose file to upload"
      />

      {isError ? (
        <p className="text-xs text-gray-600 px-1">Could not load documents</p>
      ) : docs.length > 0 ? (
        <ul className="space-y-1">
          {docs.map((name, i) => (
            <li key={i} className="flex items-center gap-1.5 text-xs text-gray-500 px-1 group">
              <FileText size={11} className="flex-shrink-0" />
              <span className="truncate flex-1">{name}</span>
              <button
                onClick={() => remove.mutate(name)}
                disabled={remove.isPending && remove.variables === name}
                className="opacity-0 group-hover:opacity-100 transition-opacity text-gray-600 hover:text-red-400 flex-shrink-0"
                aria-label={`Delete ${name}`}
              >
                {remove.isPending && remove.variables === name ? (
                  <Loader2 size={11} className="animate-spin" />
                ) : (
                  <Trash2 size={11} />
                )}
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-xs text-gray-600 px-1">
          No documents indexed yet. Upload a .txt, .md, or .pdf file.
        </p>
      )}
    </div>
  )
}
